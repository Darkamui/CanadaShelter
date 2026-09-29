using System.Globalization;
using Microsoft.Extensions.Configuration;
using Shelter.BuildingBlocks.Communications;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Communications;

/// <summary>
/// Links into the staff app, from <c>App:PublicBaseUrl</c>. Secrets go in the fragment (<c>#…</c>): browsers never
/// send it to a server, so it stays out of access logs and <c>Referer</c> headers.
/// </summary>
internal sealed class PublicLinks(IConfiguration configuration)
{
    public const string BaseUrlKey = "App:PublicBaseUrl";

    public string AcceptInvitation(string token) => Build("/accept-invitation", $"token={token}");

    public string ResetPassword(Guid userId, string token) =>
        Build("/reset-password", $"user={userId:D}&token={Uri.EscapeDataString(token)}");

    private string Build(string path, string fragment)
    {
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException($"{BaseUrlKey} must be configured to send links.");
        }

        return $"{baseUrl.TrimEnd('/')}{path}#{fragment}";
    }
}

/// <summary>
/// The Platform module's transactional emails, in the recipient's language (<c>fr</c> by default, architecture §11.3).
/// Plain text only until the Communications building block brings templates.
/// </summary>
internal static class PlatformEmails
{
    /// <summary>Lifetime of password reset tokens (Identity's data-protection tokens).</summary>
    public const int PasswordResetLifetimeHours = 2;

    public static EmailMessage Invitation(string to, string language, string organizationName, string link, DateTimeOffset expiresAt)
    {
        if (Languages.Normalize(language) == Languages.English)
        {
            var date = expiresAt.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-CA"));
            return new EmailMessage(
                to,
                $"Invitation to join {organizationName}",
                $"""
                Hello,

                You are invited to join the {organizationName} team in Shelter.

                To accept the invitation, open this link:
                {link}

                The link can be used once and expires on {date}. If you were not expecting this invitation, you can ignore this email.
                """);
        }

        var dateFr = expiresAt.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        return new EmailMessage(
            to,
            $"Invitation à rejoindre {organizationName}",
            $"""
            Bonjour,

            Nous vous invitons à rejoindre l'équipe de {organizationName} dans Shelter.

            Pour accepter l'invitation, ouvrez ce lien :
            {link}

            Le lien ne peut servir qu'une fois et expire le {dateFr}. Si vous n'attendiez pas cette invitation, vous pouvez ignorer ce courriel.
            """);
    }

    public static EmailMessage PasswordReset(string to, string language, string link)
    {
        if (Languages.Normalize(language) == Languages.English)
        {
            return new EmailMessage(
                to,
                "Reset your password",
                $"""
                Hello,

                We received a request to reset the password of your Shelter account.

                To choose a new password, open this link:
                {link}

                The link expires in {PasswordResetLifetimeHours} hours. If you did not make this request, you can ignore this email: your password stays the same.
                """);
        }

        return new EmailMessage(
            to,
            "Réinitialisation de votre mot de passe",
            $"""
            Bonjour,

            Nous avons reçu une demande de réinitialisation du mot de passe de votre compte Shelter.

            Pour choisir un nouveau mot de passe, ouvrez ce lien :
            {link}

            Le lien expire dans {PasswordResetLifetimeHours} heures. Si vous n'êtes pas à l'origine de cette demande, vous pouvez ignorer ce courriel : votre mot de passe reste le même.
            """);
    }
}
