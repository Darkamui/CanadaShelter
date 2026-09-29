using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Jobs;
using Shelter.Modules.Platform.Communications;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Identity;

namespace Shelter.Modules.Platform.Features.Session;

/// <summary>
/// Password reset by email (M2-5), on Identity's reset tokens (data protection, bound to the security stamp, valid
/// <see cref="PlatformEmails.PasswordResetLifetimeHours"/> hours):
/// <list type="bullet">
/// <item><c>POST /session/password/forgot</c>: always <c>202</c>, whether or not the email has an account; the email
/// leaves from <see cref="SendPasswordResetEmailJob"/>.</item>
/// <item><c>POST /session/password/reset</c>: sets the new password; the security stamp rotates, so every session of
/// the account ends. Recorded in <c>security_event</c>.</item>
/// </list>
/// Both are anonymous and rate limited per client IP (<see cref="RateLimitPolicies.Anonymous"/>).
/// </summary>
internal static class PasswordEndpoints
{
    public static void Map(RouteGroupBuilder session)
    {
        var password = session.MapGroup("/password");

        password.MapPost("/forgot", Forgot)
            .WithName("ForgotPlatformSessionPassword")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);

        password.MapPost("/reset", Reset)
            .WithName("ResetPlatformSessionPassword")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);
    }

    /// <summary>
    /// Emails a reset link when the email has an account. The answer never says which: same status, same body, and the
    /// email leaves from a background job enqueued either way, so the response time does not tell either.
    /// </summary>
    internal static async Task<Accepted> Forgot(
        ForgotPasswordRequest request,
        UserManager<UserAccount> userManager,
        IGlobalJobScheduler jobs)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await userManager.FindByEmailAsync(request.Email.Trim());
        jobs.Enqueue<SendPasswordResetEmailJob, SendPasswordResetEmailArgs>(new SendPasswordResetEmailArgs(user?.Id));
        return TypedResults.Accepted((string?)null);
    }

    /// <summary>A wrong, expired or already-used link and an unknown account get the same 400.</summary>
    internal static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> Reset(
        ResetPasswordRequest request,
        UserManager<UserAccount> userManager,
        SecurityEventWriter securityEvents,
        CancellationToken cancellationToken)
    {
        var user = request.UserId is { } userId && userId != Guid.Empty ? await userManager.FindByIdAsync(userId.ToString("D")) : null;
        if (user is null || string.IsNullOrEmpty(request.Token))
        {
            return InvalidLink();
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["A password is required."] });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var invalidToken = new IdentityErrorDescriber().InvalidToken().Code;
            return result.Errors.Any(e => e.Code == invalidToken)
                ? InvalidLink()
                : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [.. result.Errors.Select(e => e.Description)] });
        }

        await securityEvents.WriteAsync(user.Id, SecurityEventType.PasswordChanged, cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult InvalidLink() =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This reset link is invalid or has expired.");
}

/// <summary>Asks for a reset link.</summary>
/// <param name="Email">The account's email.</param>
internal sealed record ForgotPasswordRequest(string? Email);

/// <summary>Sets a new password from a reset link.</summary>
/// <param name="UserId">The <c>user</c> value from the link's fragment.</param>
/// <param name="Token">The <c>token</c> value from the link's fragment.</param>
/// <param name="NewPassword">The new password (12 characters or more).</param>
internal sealed record ResetPasswordRequest(Guid? UserId, string? Token, string? NewPassword);
