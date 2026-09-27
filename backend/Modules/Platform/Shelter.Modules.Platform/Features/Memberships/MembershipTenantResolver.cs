using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Identity;

namespace Shelter.Modules.Platform.Features.Memberships;

/// <summary>
/// The request's tenant is the session's active organization, but only while the account still has an active
/// membership there, checked on every request (ADR 0018). The claim alone is never trusted: a suspended or removed
/// membership resolves no tenant at once, even with a cookie issued before.
/// </summary>
internal sealed class MembershipTenantResolver : ITenantResolver
{
    public async ValueTask<Guid?> ResolveAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(context.User.FindFirstValue(PlatformClaims.ActiveOrganization), out var organizationId))
        {
            return null;
        }

        var memberships = context.RequestServices.GetRequiredService<StaffMembershipDirectory>();
        return await memberships.IsActiveMemberAsync(organizationId, context.RequestAborted) ? organizationId : null;
    }
}
