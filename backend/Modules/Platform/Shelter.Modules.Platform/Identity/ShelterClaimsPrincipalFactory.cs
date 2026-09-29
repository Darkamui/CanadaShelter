using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shelter.BuildingBlocks.Authorization;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Identity;

/// <summary>Claim types the Platform module puts in the session cookie (ADR 0018).</summary>
internal static class PlatformClaims
{
    /// <summary>The active organization; re-validated against an active membership on every request.</summary>
    public const string ActiveOrganization = "shelter:org";

    /// <summary>Present (<c>true</c>) for platform operators.</summary>
    public const string PlatformOperator = AuthorizationPolicies.PlatformOperatorClaim;

    /// <summary>Authentication methods of this session (<c>pwd</c>, <c>mfa</c>), set by the sign-in manager.</summary>
    public const string AuthenticationMethod = AuthorizationPolicies.AuthenticationMethodClaim;

    /// <summary><see cref="AuthenticationMethod"/> value of a session that signed in with a second factor.</summary>
    public const string MfaMethod = "mfa";

    /// <summary><see cref="AuthenticationMethod"/> value of a password sign-in.</summary>
    public const string PasswordMethod = "pwd";

    /// <summary>Claims that belong to the session, not the account; kept when the security stamp check rebuilds the principal.</summary>
    public static readonly IReadOnlySet<string> SessionClaims = new HashSet<string>(StringComparer.Ordinal)
    {
        ActiveOrganization,
        AuthenticationMethod,
    };
}

/// <summary>
/// The session principal: the account ID and security stamp, plus the platform-operator flag. No name or email:
/// the cookie carries nothing personal beyond the ID.
/// </summary>
internal sealed class ShelterClaimsPrincipalFactory(UserManager<UserAccount> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<UserAccount>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserAccount user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        foreach (var claim in identity.FindAll(c => c.Type == Options.ClaimsIdentity.UserNameClaimType || c.Type == Options.ClaimsIdentity.EmailClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        if (user.IsPlatformOperator)
        {
            identity.AddClaim(new Claim(PlatformClaims.PlatformOperator, "true"));
        }

        return identity;
    }
}
