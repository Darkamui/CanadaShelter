using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Records an <see cref="AuditEvent"/> for every insert, update and delete of a tenant-owned entity, in the same
/// <c>SaveChanges</c>, and so the same transaction, as the change (architecture §17.1). Registered after
/// <see cref="TenantStampingInterceptor"/>, so every audited row already has a checked tenant.
/// Updates record changed fields only. Personal values are encrypted with the data key of the entity's audit
/// subject (see <see cref="FieldClassificationExtensions.SubjectAnnotation"/>), created on first use in the same
/// transaction. Audit events themselves can only be added, never modified or deleted.
/// Global (non-tenant) tables are not audited here.
/// </summary>
internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public static AuditSaveChangesInterceptor Instance { get; } = new();

    // Data keys come from the database and the key provider, which are async; a synchronous save completes here
    // without awaiting, or fails if it would need a key.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        RecordAsync(eventData.Context, canFetchKeys: false, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await RecordAsync(eventData.Context, canFetchKeys: true, cancellationToken);
        return result;
    }

    private static async Task RecordAsync(DbContext? context, bool canFetchKeys, CancellationToken cancellationToken)
    {
        if (context is not ShelterDbContext shelter)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var keys = new AuditKeyCache(shelter, canFetchKeys);
        var events = new List<AuditEvent>();
        foreach (var entry in shelter.ChangeTracker.Entries<ITenantOwned>().ToList())
        {
            if (entry.Entity is AuditEvent)
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    throw new InvalidOperationException("Audit events are append-only; they cannot be modified or deleted.");
                }

                continue;
            }

            if (entry.Entity is PersonDataKey)
            {
                throw new InvalidOperationException("Data keys are managed by the audit key store and are never tracked.");
            }

            var (action, before, after) = Payloads(entry);
            if (action is null)
            {
                continue;
            }

            var entityType = EntityTypeName(entry.Metadata);
            var entityId = EntityId(entry);
            var subjectId = SubjectId(entry);
            if (before?.HasPersonalValues == true || after?.HasPersonalValues == true)
            {
                var protector = await keys.ProtectorAsync(entry.Entity.TenantId, subjectId, entityType, entityId, cancellationToken);
                before?.Protect(protector);
                after?.Protect(protector);
            }

            events.Add(new AuditEvent(
                entry.Entity.TenantId,
                shelter.AuditContext,
                entityType,
                entityId,
                subjectId,
                action,
                before?.ToJson(),
                after?.ToJson(),
                metadata: null,
                timestamp));
        }

        shelter.Set<AuditEvent>().AddRange(events);
    }

    // Created: every field after. Updated: changed fields only, before and after. Deleted: every field before.
    private static (string? Action, AuditPayload? Before, AuditPayload? After) Payloads(EntityEntry<ITenantOwned> entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                {
                    var after = new AuditPayload();
                    AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Current, after, before: null);
                    return (AuditActions.Created, null, after);
                }

            case EntityState.Modified:
                {
                    var before = new AuditPayload();
                    var after = new AuditPayload();
                    AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Changed, after, before);

                    // Only navigations changed: nothing to record.
                    return after.IsEmpty ? (null, null, null) : (AuditActions.Updated, before, after);
                }

            case EntityState.Deleted:
                {
                    var before = new AuditPayload();
                    AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Original, before, before: null);
                    return (AuditActions.Deleted, before, null);
                }

            default:
                return (null, null, null);
        }
    }

    // Changed: modified fields only, current values into target and original values into before.
    private static void AddFields(
        IEnumerable<PropertyEntry> properties,
        IEnumerable<ComplexPropertyEntry> complexProperties,
        FieldValues values,
        AuditPayload target,
        AuditPayload? before,
        string prefix = "")
    {
        foreach (var property in properties)
        {
            if (values == FieldValues.Changed && !property.IsModified)
            {
                continue;
            }

            var name = prefix + property.Metadata.Name;
            var classification = property.Metadata.GetFieldClassification();
            var value = values == FieldValues.Original ? property.OriginalValue : property.CurrentValue;
            target.Add(name, ToProvider(property.Metadata, value), classification);
            before?.Add(name, ToProvider(property.Metadata, property.OriginalValue), classification);
        }

        foreach (var complex in complexProperties)
        {
            AddFields(complex.Properties, complex.ComplexProperties, values, target, before, prefix + complex.Metadata.Name + ".");
        }
    }

    // The value as stored (value converters applied), so enums and strongly typed IDs read as in the table.
    private static object? ToProvider(IProperty property, object? value) =>
        value is not null && property.GetTypeMapping().Converter is { } converter
            ? converter.ConvertToProvider(value)
            : value;

    // The subject as it was for a delete, as it is otherwise.
    private static Guid? SubjectId(EntityEntry entry)
    {
        if (entry.Metadata.FindAuditSubjectProperty() is not { } subject)
        {
            return null;
        }

        var property = entry.Property(subject.Name);
        var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
        return value is Guid id && id != Guid.Empty ? id : null;
    }

    private static string EntityTypeName(IEntityType entityType) =>
        entityType.GetSchema() is { } schema
            ? schema + "." + entityType.GetTableName()
            : entityType.GetTableName() ?? entityType.Name;

    // Key values as stored; composite keys joined with '|'. Keys are client-generated, so never temporary here.
    private static string EntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Audited entity {entry.Metadata.Name} has no primary key.");

        return string.Join('|', key.Properties.Select(p => KeyValue(entry, p)));
    }

    private static string KeyValue(EntityEntry entry, IProperty keyProperty)
    {
        var property = entry.Property(keyProperty.Name);
        if (property.IsTemporary)
        {
            throw new InvalidOperationException($"Audited entity {entry.Metadata.Name} needs a client-generated key.");
        }

        return ToProvider(keyProperty, property.CurrentValue) switch
        {
            Guid guid => guid.ToString("D"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            var other => other?.ToString() ?? string.Empty,
        };
    }

    private enum FieldValues
    {
        Current,
        Original,
        Changed,
    }
}

/// <summary>
/// Chooses how personal values of one save are stored, fetching each subject's data key at most once. Without a
/// subject or a key provider they are redacted; once the subject is shredded, too.
/// </summary>
internal sealed class AuditKeyCache(ShelterDbContext db, bool canFetchKeys)
{
    private readonly Dictionary<DataKeyContext, byte[]?> _keys = [];

    public async Task<PersonalValueProtector> ProtectorAsync(
        Guid tenantId, Guid? subjectId, string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (subjectId is not { } subject || db.KeyProvider is not { } provider)
        {
            return PersonalValueProtector.NoSubject;
        }

        var context = new DataKeyContext(tenantId, subject);
        if (!_keys.TryGetValue(context, out var dataKey))
        {
            if (!canFetchKeys)
            {
                throw new InvalidOperationException("Saving personal data needs the subject's data key: use SaveChangesAsync.");
            }

            dataKey = await PersonDataKeyStore.GetOrCreateAsync(db, provider, context, cancellationToken);
            _keys[context] = dataKey;
        }

        return dataKey is null
            ? PersonalValueProtector.Shredded
            : PersonalValueProtector.Encrypting(dataKey, new AuditValueLocation(tenantId, subject, entityType, entityId));
    }
}
