using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Identity;

/// <summary>Appends <see cref="SecurityEvent"/>s (ADR 0018). Joins the request's transaction when there is one.</summary>
internal sealed class SecurityEventWriter(ShelterDbContext db, AuditContext auditContext, TimeProvider timeProvider)
{
    public async Task WriteAsync(Guid? userId, SecurityEventType type, CancellationToken cancellationToken)
    {
        db.Set<SecurityEvent>().Add(new SecurityEvent(userId, type, timeProvider.GetUtcNow(), auditContext.CorrelationId));
        await db.SaveChangesAsync(cancellationToken);
    }
}
