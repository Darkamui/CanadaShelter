using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Features.Memberships;

namespace Shelter.Modules.Platform.Identity;

/// <summary>
/// Adds the active organization to every sign-in (ADR 0018): the one already chosen when the same account signs in
/// again (password or MFA change), otherwise the account's only active membership. With several, none is chosen
/// until <c>POST /session/organization/{id}</c>. The resolver re-checks the choice on every request.
/// </summary>
internal sealed class ShelterSignInManager(
    UserManager<UserAccount> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<UserAccount> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<UserAccount>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<UserAccount> confirmation,
    IServiceScopeFactory scopeFactory)
    : SignInManager<UserAccount>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task SignInWithClaimsAsync(UserAccount user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = additionalClaims.ToList();
        if (!claims.Exists(c => c.Type == PlatformClaims.ActiveOrganization) && await DefaultOrganizationAsync(user) is { } organizationId)
        {
            claims.Add(new Claim(PlatformClaims.ActiveOrganization, organizationId.ToString("D")));
        }

        await base.SignInWithClaimsAsync(user, authenticationProperties, claims);
    }

    private async Task<Guid?> DefaultOrganizationAsync(UserAccount user)
    {
        var current = Context.User;
        if (Guid.TryParse(current.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
            && currentUserId == user.Id
            && Guid.TryParse(current.FindFirstValue(PlatformClaims.ActiveOrganization), out var kept))
        {
            // Not re-checked here: the claim is only a choice, and MembershipTenantResolver validates it per request.
            return kept;
        }

        // Its own scope: at login the request has no account yet, and must not get one before the cookie exists.
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<UserContext>().Set(user.Id);
        var memberships = await scope.ServiceProvider.GetRequiredService<StaffMembershipDirectory>().ListActiveAsync(Context.RequestAborted);
        return memberships.Count == 1 ? memberships[0].OrganizationId : null;
    }
}
