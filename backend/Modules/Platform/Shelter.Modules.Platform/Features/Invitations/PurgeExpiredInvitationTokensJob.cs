using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Jobs;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Invitations;

/// <summary>
/// Deletes the global <see cref="InvitationToken"/> rows of expired invitations, hourly. Accept, revoke and resend
/// delete their own token; this catches invitations nobody acted on. The invitation itself stays (it shows as expired
/// and can be resent).
/// </summary>
internal sealed class PurgeExpiredInvitationTokensJob(ShelterDbContext db, TimeProvider time) : IGlobalJob<NoJobArgs>
{
    public const string RecurringId = "platform:purge-expired-invitation-tokens";

    public Task ExecuteAsync(NoJobArgs args, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return db.Set<InvitationToken>().Where(t => t.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }
}
