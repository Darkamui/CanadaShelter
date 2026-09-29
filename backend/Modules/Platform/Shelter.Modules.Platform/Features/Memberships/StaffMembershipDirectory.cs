using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Memberships;

/// <summary>
/// The signed-in account's own active memberships, across organizations. Reads through the self-read policy
/// (<c>app.user_id</c>), so it works before an organization is chosen; it never sees another account's rows.
/// Scoped.
/// </summary>
internal sealed class StaffMembershipDirectory(ShelterDbContext db, UnitOfWork unitOfWork, IUserContext userContext)
{
    /// <summary>Whether the current account has an active membership in <paramref name="organizationId"/>.</summary>
    public async Task<bool> IsActiveMemberAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return false;
        }

        return await unitOfWork.ExecuteAsync(
            ct => ActiveMemberships(userId).AnyAsync(m => m.TenantId == organizationId, ct),
            static _ => true,
            cancellationToken);
    }

    /// <summary>
    /// The role keys of the current account's active membership in <paramref name="organizationId"/>, or
    /// <c>null</c> without one. May be empty: a member without roles has no permissions.
    /// </summary>
    public async Task<string[]?> FindActiveRoleKeysAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return null;
        }

        return await unitOfWork.ExecuteAsync(
            ct => ActiveMemberships(userId)
                .Where(m => m.TenantId == organizationId)
                .Select(m => m.RoleKeys)
                .SingleOrDefaultAsync(ct),
            static _ => true,
            cancellationToken);
    }

    /// <summary>The current account's active memberships, by organization name. Empty when anonymous.</summary>
    public async Task<IReadOnlyList<MembershipSummary>> ListActiveAsync(CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return [];
        }

        return await unitOfWork.ExecuteAsync(
            ct => ActiveMemberships(userId)
                .Join(db.Set<Organization>(), m => m.TenantId, o => o.Id, (m, o) => o)
                .OrderBy(o => o.Name)
                .Select(o => new MembershipSummary(o.Id, o.Name))
                .ToListAsync(ct),
            static _ => true,
            cancellationToken);
    }

    // The tenant filter is ignored on purpose: these rows span organizations, and RLS (self-read) scopes them to
    // the account. The user condition is repeated so a tenant-scoped transaction never widens the result.
    private IQueryable<StaffMembership> ActiveMemberships(Guid userId) =>
        db.Set<StaffMembership>()
            .IgnoreQueryFilters([ShelterDbContext.TenantFilter])
            .Where(m => m.UserId == userId && m.Status == StaffMembershipStatus.Active);
}

/// <summary>An organization the account may select.</summary>
/// <param name="OrganizationId">Organization ID.</param>
/// <param name="OrganizationName">Organization name.</param>
internal sealed record MembershipSummary(Guid OrganizationId, string OrganizationName);
