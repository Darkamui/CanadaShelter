using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Identity;

namespace Shelter.Modules.Platform.Features.Memberships;

/// <summary>
/// The request's tenant is the session's active organization, but only while the account still has an active
/// membership there, checked on every request (ADR 0018). The claim alone is never trusted: a suspended or removed
/// membership resolves no tenant at once, even with a cookie issued before. The same read yields the membership's
/// roles, turned into the request's permissions: a role change also applies on the next request. Administrators and
/// platform operators must have signed in with MFA (M2-4); until then, the session must enroll and no tenant endpoint
/// is reachable (<see cref="IMfaContext"/>).
/// </summary>
internal sealed class MembershipTenantResolver : ITenantResolver
{
    public async ValueTask<Guid?> ResolveAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var services = context.RequestServices;
        var mfaEnforced = services.GetRequiredService<MfaEnforcement>().Enforced;
        var signedInWithMfa = user.HasClaim(PlatformClaims.AuthenticationMethod, PlatformClaims.MfaMethod);
        var mfaContext = services.GetRequiredService<MfaContext>();
        var isOperator = user.HasClaim(PlatformClaims.PlatformOperator, "true");
        mfaContext.Set(mfaEnforced && isOperator && !signedInWithMfa);

        if (!Guid.TryParse(user.FindFirstValue(PlatformClaims.ActiveOrganization), out var organizationId))
        {
            return null;
        }

        var roleKeys = await services.GetRequiredService<StaffMembershipDirectory>().FindActiveRoleKeysAsync(organizationId, context.RequestAborted);
        if (roleKeys is null)
        {
            return null;
        }

        var mustUseMfa = isOperator || roleKeys.Contains(SystemRoles.Administrator, StringComparer.Ordinal);
        mfaContext.Set(mfaEnforced && mustUseMfa && !signedInWithMfa);
        services.GetRequiredService<PermissionContext>().Set(services.GetRequiredService<PermissionCatalog>().PermissionsFor(roleKeys));
        return organizationId;
    }
}
