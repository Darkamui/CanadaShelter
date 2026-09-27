using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Reads the audit history of the current tenant with personal values decrypted. Callers must check the reader's
/// permission first (TODO(M2): audit-read permission); personal values must never be logged.
/// </summary>
public interface IAuditReader
{
    /// <summary>
    /// Every event of one entity, oldest first. Encrypted values whose subject was shredded read as
    /// <c>{"$unrecoverable":"shredded"}</c>. Run it inside a unit of work.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> GetEntityHistoryAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}

/// <summary>One audit event as read back: payloads hold plain values where the reader could decrypt them.</summary>
public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset TimestampUtc,
    AuditActorType ActorType,
    Guid? ActorId,
    string Source,
    string? CorrelationId,
    string EntityType,
    string EntityId,
    Guid? SubjectId,
    string Action,
    JsonObject? Before,
    JsonObject? After,
    JsonObject? Metadata);

/// <summary><see cref="IAuditReader"/> on the scoped <see cref="ShelterDbContext"/>. Scoped.</summary>
internal sealed class AuditReader(ShelterDbContext db) : IAuditReader
{
    /// <summary>Marker replacing a value that can no longer be decrypted.</summary>
    public const string UnrecoverableMarker = "$unrecoverable";

    public async Task<IReadOnlyList<AuditEntry>> GetEntityHistoryAsync(
        string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var tenantId = db.TenantContext.RequireTenantId();
        var events = await db.Set<AuditEvent>()
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.TimestampUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var keys = new Dictionary<Guid, byte[]?>();
        var entries = new List<AuditEntry>(events.Count);
        foreach (var e in events)
        {
            byte[]? dataKey = null;
            if (e.SubjectId is { } subject && db.KeyProvider is { } provider && !keys.TryGetValue(subject, out dataKey))
            {
                dataKey = await PersonDataKeyStore.FindKeyAsync(db, provider, new DataKeyContext(tenantId, subject), cancellationToken);
                keys[subject] = dataKey;
            }

            var location = new AuditValueLocation(tenantId, e.SubjectId ?? Guid.Empty, e.EntityType, e.EntityId);
            entries.Add(new AuditEntry(
                e.Id,
                e.TimestampUtc,
                e.ActorType,
                e.ActorId,
                e.Source,
                e.CorrelationId,
                e.EntityType,
                e.EntityId,
                e.SubjectId,
                e.Action,
                Decrypt(e.BeforeJson, dataKey, location),
                Decrypt(e.AfterJson, dataKey, location),
                e.Metadata is null ? null : JsonNode.Parse(e.Metadata)?.AsObject()));
        }

        return entries;
    }

    private static JsonObject? Decrypt(string? json, byte[]? dataKey, AuditValueLocation location)
    {
        if (json is null || JsonNode.Parse(json) is not JsonObject payload)
        {
            return null;
        }

        foreach (var (field, value) in payload.ToList())
        {
            if (value is JsonObject marker && marker[PersonalValueProtector.EncryptedMarker]?.GetValue<string>() is { } encrypted)
            {
                payload[field] = dataKey is null
                    ? new JsonObject { [UnrecoverableMarker] = "shredded" }
                    : PersonalValueProtector.Decrypt(dataKey, location, field, encrypted);
            }
        }

        return payload;
    }
}
