using System.Text.Json;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Records audit events for changes EF does not track: raw SQL, bulk operations, integrations, and actions
/// that are not row changes (an export, a view of sensitive data). Uses the same classification rules and the
/// same actor, source and correlation as tracked changes.
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Adds the event and saves it for the current tenant. Call it inside the unit of work that made the change,
    /// so both commit or roll back together. Saves any other pending changes of the scope's context too.
    /// </summary>
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

/// <summary>One field of an explicit audit record, with its classification.</summary>
public sealed record AuditField(string Name, object? Value, FieldClassification Classification)
{
    /// <summary>A field whose value is not personal information and is stored in plain form.</summary>
    public static AuditField NonPersonal(string name, object? value) => new(name, value, FieldClassification.NonPersonal);

    /// <summary>A field whose value is personal information and is never stored in plain form.</summary>
    public static AuditField Personal(string name, object? value) => new(name, value, FieldClassification.Personal);
}

/// <summary>An explicit audit record.</summary>
/// <param name="EntityType">What was acted on, as <c>schema.table</c> or another stable name.</param>
/// <param name="EntityId">Its ID (key values, never personal data).</param>
/// <param name="Action">What happened, e.g. <see cref="AuditActions.Updated"/> or <c>Exported</c>.</param>
public sealed record AuditRecord(string EntityType, string EntityId, string Action)
{
    /// <summary>Field values before the action.</summary>
    public IReadOnlyList<AuditField> Before { get; init; } = [];

    /// <summary>Field values after the action.</summary>
    public IReadOnlyList<AuditField> After { get; init; } = [];

    /// <summary>Extra context: codes and IDs only, never personal data (stored in plain form).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary><see cref="IAuditWriter"/> on the scoped <see cref="ShelterDbContext"/>. Scoped.</summary>
internal sealed class AuditWriter(ShelterDbContext db) : IAuditWriter
{
    public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.EntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Action);

        db.Set<AuditEvent>().Add(new AuditEvent(
            db.TenantContext.RequireTenantId(),
            db.AuditContext,
            record.EntityType,
            record.EntityId,
            record.Action,
            ToJson(record.Before),
            ToJson(record.After),
            record.Metadata is { Count: > 0 } metadata ? JsonSerializer.Serialize(metadata) : null,
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? ToJson(IReadOnlyList<AuditField> fields)
    {
        var payload = new AuditPayload();
        foreach (var field in fields)
        {
            payload.Add(field.Name, field.Value, field.Classification);
        }

        return payload.ToJson();
    }
}
