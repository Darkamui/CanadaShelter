using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shelter.BuildingBlocks.Communications;
using Shelter.BuildingBlocks.Jobs;
using Shelter.Modules.Platform.Communications;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Identity;

namespace Shelter.Modules.Platform.Features.Session;

/// <summary>
/// Emails a password reset link (M2-8). <c>POST /session/password/forgot</c> enqueues this job for every request, with
/// no user when the email has no account, so both answers take the same path and time. The token is generated here,
/// when the email leaves, so a queued job never shortens the link's lifetime.
/// </summary>
internal sealed partial class SendPasswordResetEmailJob(
    UserManager<UserAccount> userManager,
    IEmailSender emailSender,
    PublicLinks links,
    ILogger<SendPasswordResetEmailJob> logger) : IGlobalJob<SendPasswordResetEmailArgs>
{
    public async Task ExecuteAsync(SendPasswordResetEmailArgs args, CancellationToken cancellationToken)
    {
        var user = args.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString("D")) : null;
        if (user?.Email is null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        try
        {
            await emailSender.SendAsync(PlatformEmails.PasswordReset(user.Email, user.PreferredLanguage, links.ResetPassword(user.Id, token)), cancellationToken);
        }
        catch (Exception exception) when (exception is System.Net.Mail.SmtpException or InvalidOperationException or IOException)
        {
            // Not rethrown: Hangfire would store the exception, whose message can name the address. The user can ask again.
            LogSendFailed(logger, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset email could not be sent ({ExceptionType}).")]
    private static partial void LogSendFailed(ILogger logger, string exceptionType);
}

/// <summary>The account to email, or none. An ID only: Hangfire stores job arguments in plain tables.</summary>
/// <param name="UserId">The account, or <c>null</c> when the email has none.</param>
internal sealed record SendPasswordResetEmailArgs(Guid? UserId);
