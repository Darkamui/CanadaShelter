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
/// Updates record changed fields only. Audit events themselves can only be added, never modified or deleted.
/// Global (non-tenant) tables are not audited here.
/// </summary>
internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public static AuditSaveChangesInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Record(DbContext? context)
    {
        if (context is not ShelterDbContext shelter)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var events = new List<AuditEvent>();
        foreach (var entry in shelter.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.Entity is AuditEvent)
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    throw new InvalidOperationException("Audit events are append-only; they cannot be modified or deleted.");
                }

                continue;
            }

            var auditEvent = entry.State switch
            {
                EntityState.Added => Created(shelter, entry, timestamp),
                EntityState.Modified => Updated(shelter, entry, timestamp),
                EntityState.Deleted => Deleted(shelter, entry, timestamp),
                _ => null,
            };

            if (auditEvent is not null)
            {
                events.Add(auditEvent);
            }
        }

        shelter.Set<AuditEvent>().AddRange(events);
    }

    private static AuditEvent Created(ShelterDbContext db, EntityEntry<ITenantOwned> entry, DateTimeOffset timestamp)
    {
        var after = new AuditPayload();
        AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Current, after, before: null);
        return Create(db, entry, AuditActions.Created, before: null, after, timestamp);
    }

    private static AuditEvent? Updated(ShelterDbContext db, EntityEntry<ITenantOwned> entry, DateTimeOffset timestamp)
    {
        var before = new AuditPayload();
        var after = new AuditPayload();
        AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Changed, after, before);

        // Only navigations changed: nothing to record.
        return after.IsEmpty ? null : Create(db, entry, AuditActions.Updated, before, after, timestamp);
    }

    private static AuditEvent Deleted(ShelterDbContext db, EntityEntry<ITenantOwned> entry, DateTimeOffset timestamp)
    {
        var before = new AuditPayload();
        AddFields(entry.Properties, entry.ComplexProperties, FieldValues.Original, before, before: null);
        return Create(db, entry, AuditActions.Deleted, before, after: null, timestamp);
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

    private static AuditEvent Create(
        ShelterDbContext db, EntityEntry<ITenantOwned> entry, string action, AuditPayload? before, AuditPayload? after, DateTimeOffset timestamp) =>
        new(
            entry.Entity.TenantId,
            db.AuditContext,
            EntityTypeName(entry.Metadata),
            EntityId(entry),
            action,
            before?.ToJson(),
            after?.ToJson(),
            metadata: null,
            timestamp);

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
