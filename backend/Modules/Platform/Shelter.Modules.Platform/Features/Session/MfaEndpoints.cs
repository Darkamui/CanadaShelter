using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Shelter.BuildingBlocks.Authorization;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Identity;

namespace Shelter.Modules.Platform.Features.Session;

/// <summary>
/// TOTP multi-factor authentication (M2-4, ADR 0018), on Identity's authenticator and recovery codes:
/// <list type="bullet">
/// <item><c>POST /session/login/mfa</c>: the second login step, after a password login answered <c>mfaRequired</c>.
/// Takes an authenticator code or a single-use recovery code.</item>
/// <item><c>POST /session/mfa/setup</c>, <c>/enable</c>, <c>/disable</c>: enrollment and removal, for the signed-in
/// account. Reachable without an organization, so administrators who must enroll can.</item>
/// </list>
/// Enrollment and removal rotate the security stamp (other sessions end) and re-issue this session's cookie. Both
/// are recorded in <c>security_event</c>. Wrong codes count toward the account lockout.
/// </summary>
internal static class MfaEndpoints
{
    /// <summary>Recovery codes issued at enrollment; each works once.</summary>
    public const int RecoveryCodeCount = 10;

    /// <summary>Issuer shown in authenticator apps.</summary>
    public const string Issuer = "Shelter";

    public static void Map(RouteGroupBuilder session)
    {
        session.MapPost("/login/mfa", Login)
            .WithName("LoginPlatformSessionMfa")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);

        var mfa = session.MapGroup("/mfa");

        mfa.MapPost("/setup", Setup)
            .WithName("SetupPlatformSessionMfa")
            .RequireSession();

        mfa.MapPost("/enable", Enable)
            .WithName("EnablePlatformSessionMfa")
            .RequireSession();

        mfa.MapPost("/disable", Disable)
            .WithName("DisablePlatformSessionMfa")
            .RequireSession();
    }

    /// <summary>
    /// Completes a login whose password was verified (the short-lived two-factor cookie). Without that cookie, or
    /// with a wrong code, the answer is the same 401.
    /// </summary>
    internal static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> Login(
        LoginMfaRequest request,
        HttpContext httpContext,
        SignInManager<UserAccount> signInManager,
        SecurityEventWriter securityEvents,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return InvalidCode(StatusCodes.Status401Unauthorized);
        }

        var recovery = string.IsNullOrWhiteSpace(request.Code) && !string.IsNullOrWhiteSpace(request.RecoveryCode);
        var result = recovery
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(request.RecoveryCode!.Trim().ToUpperInvariant())
            : await signInManager.TwoFactorAuthenticatorSignInAsync(Normalize(request.Code), isPersistent: false, rememberClient: false);

        if (!result.Succeeded)
        {
            await securityEvents.WriteAsync(user.Id, result.IsLockedOut ? SecurityEventType.LockedOut : SecurityEventType.LoginFailed, cancellationToken);
            return InvalidCode(StatusCodes.Status401Unauthorized);
        }

        if (recovery)
        {
            await securityEvents.WriteAsync(user.Id, SecurityEventType.RecoveryCodeUsed, cancellationToken);
        }

        await securityEvents.WriteAsync(user.Id, SecurityEventType.LoginSucceeded, cancellationToken);

        // The antiforgery token is bound to the user: reissue it for the new principal.
        httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        AntiforgeryTokens.Issue(httpContext);
        return TypedResults.Ok(new LoginResponse(LoginStatus.SignedIn));
    }

    /// <summary>
    /// A new authenticator key for the account, to scan or type into an app. Nothing is enforced until
    /// <c>/enable</c> confirms a code; calling it again replaces the key.
    /// </summary>
    internal static async Task<Results<Ok<MfaSetupResponse>, ProblemHttpResult, UnauthorizedHttpResult>> Setup(
        ClaimsPrincipal principal,
        UserManager<UserAccount> userManager,
        SignInManager<UserAccount> signInManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return AlreadyEnabled();
        }

        // Also rotates the security stamp; keep this session.
        await userManager.ResetAuthenticatorKeyAsync(user);
        await ReSignInAsync(signInManager, user, principal.HasClaim(PlatformClaims.AuthenticationMethod, PlatformClaims.MfaMethod) ? PlatformClaims.MfaMethod : PlatformClaims.PasswordMethod);

        var key = (await userManager.GetAuthenticatorKeyAsync(user))!;
        var account = Uri.EscapeDataString(user.Email ?? user.Id.ToString("D"));
        var issuer = Uri.EscapeDataString(Issuer);
        return TypedResults.Ok(new MfaSetupResponse(key, $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6"));
    }

    /// <summary>Turns MFA on once a code from the new key checks out; returns the recovery codes, shown only now.</summary>
    internal static async Task<Results<Ok<MfaRecoveryCodesResponse>, ProblemHttpResult, UnauthorizedHttpResult>> Enable(
        MfaCodeRequest request,
        ClaimsPrincipal principal,
        UserManager<UserAccount> userManager,
        SignInManager<UserAccount> signInManager,
        SecurityEventWriter securityEvents,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return AlreadyEnabled();
        }

        if (await userManager.GetAuthenticatorKeyAsync(user) is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Start MFA setup first.");
        }

        if (!await VerifyAsync(userManager, user, request.Code))
        {
            return InvalidCode(StatusCodes.Status400BadRequest);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await securityEvents.WriteAsync(user.Id, SecurityEventType.MfaEnabled, cancellationToken);

        // The code just verified is this session's second factor.
        await ReSignInAsync(signInManager, user, PlatformClaims.MfaMethod);
        return TypedResults.Ok(new MfaRecoveryCodesResponse([.. codes!]));
    }

    /// <summary>Turns MFA off; requires a current authenticator code. Administrators must then enroll again.</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> Disable(
        MfaCodeRequest request,
        ClaimsPrincipal principal,
        UserManager<UserAccount> userManager,
        SignInManager<UserAccount> signInManager,
        SecurityEventWriter securityEvents,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!user.TwoFactorEnabled)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "MFA is not enabled.");
        }

        if (!await VerifyAsync(userManager, user, request.Code))
        {
            return InvalidCode(StatusCodes.Status400BadRequest);
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await securityEvents.WriteAsync(user.Id, SecurityEventType.MfaDisabled, cancellationToken);

        await ReSignInAsync(signInManager, user, PlatformClaims.PasswordMethod);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Checks an authenticator code. A locked-out account is refused without checking; a wrong code counts toward
    /// the lockout, so a stolen session cannot guess its way to disabling MFA.
    /// </summary>
    private static async Task<bool> VerifyAsync(UserManager<UserAccount> userManager, UserAccount user, string? code)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        if (await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, Normalize(code)))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return true;
        }

        await userManager.AccessFailedAsync(user);
        return false;
    }

    /// <summary>A new cookie for this session with the new security stamp; the active organization is kept.</summary>
    private static Task ReSignInAsync(SignInManager<UserAccount> signInManager, UserAccount user, string method) =>
        signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim(PlatformClaims.AuthenticationMethod, method)]);

    /// <summary>Authenticator codes are digits; apps often show them as <c>123 456</c>.</summary>
    private static string Normalize(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    private static ProblemHttpResult InvalidCode(int statusCode) =>
        TypedResults.Problem(statusCode: statusCode, title: "Invalid code.");

    private static ProblemHttpResult AlreadyEnabled() =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "MFA is already enabled.");
}

/// <summary>Second login step: one of the two codes.</summary>
/// <param name="Code">Six-digit code from the authenticator app.</param>
/// <param name="RecoveryCode">A recovery code, used instead of <paramref name="Code"/>; works once.</param>
internal sealed record LoginMfaRequest(string? Code, string? RecoveryCode);

/// <summary>An authenticator code.</summary>
/// <param name="Code">Six-digit code from the authenticator app.</param>
internal sealed record MfaCodeRequest(string? Code);

/// <summary>A new authenticator key.</summary>
/// <param name="SharedKey">Base32 key, to type into the app.</param>
/// <param name="AuthenticatorUri"><c>otpauth://</c> URI, to show as a QR code.</param>
internal sealed record MfaSetupResponse(string SharedKey, string AuthenticatorUri);

/// <summary>Single-use recovery codes, shown only once.</summary>
/// <param name="RecoveryCodes">The codes.</param>
internal sealed record MfaRecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
