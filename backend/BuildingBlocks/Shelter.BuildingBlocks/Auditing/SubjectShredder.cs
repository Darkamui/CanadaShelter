using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Crypto-shredding (ADR 0011): destroys a person's data key, so every personal value encrypted with it becomes
/// unrecoverable while the audit rows stay (actor, action, field names, timestamps). Used by the purge and
/// anonymization flows (Law 25), inside their unit of work. Irreversible once committed.
/// </summary>
public interface ISubjectShredder
{
    /// <summary>
    /// Shreds <paramref name="subjectId"/>'s key in the current tenant and records a <c>Shredded</c> audit event.
    /// Later personal values for the subject are recorded as redacted, never under a new key. Idempotent.
    /// </summary>
    Task ShredSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default);
}

/// <summary><see cref="ISubjectShredder"/> on the scoped <see cref="ShelterDbContext"/>. Scoped.</summary>
internal sealed class SubjectShredder(ShelterDbContext db, IAuditWriter audit) : ISubjectShredder
{
    public const string Shredded = "Shredded";

    public async Task ShredSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("A subject is required.", nameof(subjectId));
        }

        var tenantId = db.TenantContext.RequireTenantId();
        await PersonDataKeyStore.ShredAsync(db, new DataKeyContext(tenantId, subjectId), DateTimeOffset.UtcNow, cancellationToken);

        await audit.WriteAsync(
            new AuditRecord($"{AuditEvent.Schema}.{PersonDataKey.Table}", subjectId.ToString("D"), Shredded) { SubjectId = subjectId },
            cancellationToken);
    }
}
