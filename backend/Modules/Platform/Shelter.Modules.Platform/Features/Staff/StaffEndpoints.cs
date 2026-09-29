using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Authorization;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Features.Staff;

/// <summary>
/// <c>/api/platform/staff</c>: the organization's staff memberships, their roles and status (M2-3). Every change is an
/// update of a tenant-owned row, so the audit interceptor records it (who changed which roles, when). The last active
/// administrator can be neither demoted nor suspended. IDs from another organization are not found (RLS and filter).
/// </summary>
internal static class StaffEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapGroup("/staff");

        staff.MapGet("", List)
            .WithName("ListPlatformStaff")
            .RequirePermission(PlatformPermissions.StaffRead);

        staff.MapPut("/{membershipId:guid}/roles", ChangeRoles)
            .WithName("ChangePlatformStaffRoles")
            .RequirePermission(PlatformPermissions.StaffManage);

        staff.MapPost("/{membershipId:guid}/suspend", Suspend)
            .WithName("SuspendPlatformStaff")
            .RequirePermission(PlatformPermissions.StaffManage);

        staff.MapPost("/{membershipId:guid}/reactivate", Reactivate)
            .WithName("ReactivatePlatformStaff")
            .RequirePermission(PlatformPermissions.StaffManage);
    }

    internal static async Task<Ok<IReadOnlyList<StaffMemberResponse>>> List(ShelterDbContext db, CancellationToken cancellationToken)
    {
        // Accounts are global; the membership (tenant filter and RLS) decides which ones this organization sees.
        var rows = await db.Set<StaffMembership>()
            .AsNoTracking()
            .Join(db.Set<UserAccount>(), m => m.UserId, u => u.Id, (m, u) => new { Membership = m, Account = u })
            .OrderBy(r => r.Account.DisplayName)
            .ThenBy(r => r.Membership.Id)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<StaffMemberResponse>>([.. rows.Select(r => new StaffMemberResponse(
            r.Membership.Id,
            r.Membership.UserId,
            r.Account.DisplayName,
            r.Account.Email ?? string.Empty,
            r.Membership.Status.ToString(),
            r.Membership.RoleKeys))]);
    }

    internal static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> ChangeRoles(
        Guid membershipId,
        ChangeStaffRolesRequest request,
        ShelterDbContext db,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!StaffMembership.AreValid(request.Roles))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["roles"] = [$"One or more of: {string.Join(", ", SystemRoles.All.Order(StringComparer.Ordinal))}."],
            });
        }

        var roles = request.Roles!;
        return await ChangeAsync(
            membershipId,
            db,
            tenantContext,
            losesAdministrator: m => m.IsActiveAdministrator && !roles.Contains(SystemRoles.Administrator, StringComparer.Ordinal),
            change: m => m.ChangeRoles(roles),
            cancellationToken);
    }

    internal static Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> Suspend(
        Guid membershipId, ShelterDbContext db, ITenantContext tenantContext, CancellationToken cancellationToken) =>
        ChangeAsync(membershipId, db, tenantContext, m => m.IsActiveAdministrator, m => m.Suspend(), cancellationToken);

    internal static Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> Reactivate(
        Guid membershipId, ShelterDbContext db, ITenantContext tenantContext, CancellationToken cancellationToken) =>
        ChangeAsync(membershipId, db, tenantContext, static _ => false, m => m.Reactivate(), cancellationToken);

    private static async Task<Results<NoContent, NotFound, ValidationProblem, ProblemHttpResult>> ChangeAsync(
        Guid membershipId,
        ShelterDbContext db,
        ITenantContext tenantContext,
        Func<StaffMembership, bool> losesAdministrator,
        Action<StaffMembership> change,
        CancellationToken cancellationToken)
    {
        // Lock the organization's active administrators first (FOR UPDATE, in the endpoint's transaction): two
        // concurrent demotions serialize here, and the second one sees the first one's result.
        var tenantId = tenantContext.RequireTenantId();
        var administrators = await db.Database
            .SqlQuery<Guid>(
                $"""
                SELECT id AS "Value" FROM platform.staff_membership
                WHERE tenant_id = {tenantId} AND status = 'Active' AND 'administrator' = ANY (role_keys)
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        var membership = await db.Set<StaffMembership>().SingleOrDefaultAsync(m => m.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        if (losesAdministrator(membership) && administrators.Count <= 1)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The organization must keep at least one active administrator.");
        }

        change(membership);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

/// <summary>New roles of a staff membership.</summary>
/// <param name="Roles">System role keys: <c>administrator</c>, <c>staff</c>, <c>read_only</c>. At least one.</param>
internal sealed record ChangeStaffRolesRequest(IReadOnlyList<string>? Roles);

/// <summary>A staff member of the organization.</summary>
/// <param name="MembershipId">Membership ID (the one to manage).</param>
/// <param name="UserId">Account ID.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="Email">Sign-in email.</param>
/// <param name="Status"><c>Active</c> or <c>Suspended</c>.</param>
/// <param name="Roles">System role keys.</param>
internal sealed record StaffMemberResponse(Guid MembershipId, Guid UserId, string DisplayName, string Email, string Status, IReadOnlyList<string> Roles);
