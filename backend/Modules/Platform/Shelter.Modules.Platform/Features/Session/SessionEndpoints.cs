using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
/// <c>/api/platform/session</c>: cookie login, logout and the current session (ADR 0018). Every unsafe call,
/// login included, carries the antiforgery header (the group's filter).
/// </summary>
internal static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var session = endpoints.MapGroup("/session");

        session.MapGet("/antiforgery", IssueAntiforgery)
            .WithName("GetPlatformSessionAntiforgery")
            .AllowAnonymous();

        session.MapPost("/login", Login)
            .WithName("LoginPlatformSession")
            .AllowAnonymous();

        session.MapPost("/logout", Logout)
            .WithName("LogoutPlatformSession")
            .RequireSession();

        session.MapGet("", GetSession)
            .WithName("GetPlatformSession")
            .RequireSession();
    }

    /// <summary>Sets the readable <c>XSRF-TOKEN</c> cookie the SPA echoes in <c>X-XSRF-TOKEN</c>.</summary>
    internal static NoContent IssueAntiforgery(HttpContext httpContext)
    {
        AntiforgeryTokens.Issue(httpContext);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> Login(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<UserAccount> userManager,
        SignInManager<UserAccount> signInManager,
        SecurityEventWriter securityEvents,
        CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Same work as a wrong password, so the response time does not reveal whether the account exists.
            DummyPassword.Verify(userManager, request.Password);
            await securityEvents.WriteAsync(null, SecurityEventType.LoginFailed, cancellationToken);
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password ?? string.Empty, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await securityEvents.WriteAsync(user.Id, SecurityEventType.LoginSucceeded, cancellationToken);

            // The antiforgery token is bound to the user: reissue it for the new principal.
            httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
            AntiforgeryTokens.Issue(httpContext);
            return TypedResults.Ok(new LoginResponse(LoginStatus.SignedIn));
        }

        if (result.RequiresTwoFactor)
        {
            // The password was right; the two-factor cookie now waits for the code (M2-4).
            return TypedResults.Ok(new LoginResponse(LoginStatus.MfaRequired));
        }

        // Locked out, not allowed or wrong password: one answer for all of them.
        await securityEvents.WriteAsync(user.Id, result.IsLockedOut ? SecurityEventType.LockedOut : SecurityEventType.LoginFailed, cancellationToken);
        return InvalidCredentials();
    }

    internal static async Task<NoContent> Logout(HttpContext httpContext, SecurityEventWriter securityEvents, CancellationToken cancellationToken)
    {
        var userId = UserId(httpContext.User);

        // Not SignInManager.SignOutAsync: no external scheme is registered.
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await httpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await securityEvents.WriteAsync(userId, SecurityEventType.LoggedOut, cancellationToken);

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        AntiforgeryTokens.Issue(httpContext);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> GetSession(
        ClaimsPrincipal principal,
        UserManager<UserAccount> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new SessionResponse(
            new SessionUser(user.Id, user.DisplayName, user.PreferredLanguage, user.IsPlatformOperator, user.TwoFactorEnabled)));
    }

    private static ProblemHttpResult InvalidCredentials() =>
        TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

    private static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>A hash of a random password, verified when the email is unknown.</summary>
    private static class DummyPassword
    {
        private static string? _hash;

        public static void Verify(UserManager<UserAccount> userManager, string? password)
        {
            var dummy = new UserAccount("dummy@invalid", "dummy", Languages.French, DateTimeOffset.UnixEpoch);
            _hash ??= userManager.PasswordHasher.HashPassword(dummy, Guid.NewGuid().ToString("N"));
            userManager.PasswordHasher.VerifyHashedPassword(dummy, _hash, password ?? string.Empty);
        }
    }
}

/// <summary>Login request.</summary>
/// <param name="Email">Account email.</param>
/// <param name="Password">Account password.</param>
/// <remarks>Missing fields bind as null and get the same 401 as a wrong password.</remarks>
internal sealed record LoginRequest(string Email, string Password);

/// <summary>Values of <see cref="LoginResponse.Status"/>.</summary>
internal static class LoginStatus
{
    /// <summary>The session cookie is set.</summary>
    public const string SignedIn = "signedIn";

    /// <summary>The password was right; a second factor is required.</summary>
    public const string MfaRequired = "mfaRequired";
}

/// <summary>Login result.</summary>
/// <param name="Status"><c>signedIn</c> or <c>mfaRequired</c>.</param>
internal sealed record LoginResponse(string Status);

/// <summary>The current session.</summary>
/// <param name="User">The signed-in account.</param>
internal sealed record SessionResponse(SessionUser User);

/// <summary>The signed-in account.</summary>
/// <param name="Id">Account ID.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="PreferredLanguage"><c>fr</c> or <c>en</c>.</param>
/// <param name="IsPlatformOperator">Whether the account operates the platform.</param>
/// <param name="MfaEnabled">Whether a second factor is enrolled.</param>
internal sealed record SessionUser(Guid Id, string DisplayName, string PreferredLanguage, bool IsPlatformOperator, bool MfaEnabled);
