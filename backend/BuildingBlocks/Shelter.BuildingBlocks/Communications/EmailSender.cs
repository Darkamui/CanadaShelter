using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shelter.BuildingBlocks.Communications;

/// <summary>
/// An email to one recipient. Holds personal data (the address, often a name in the body): never log it.
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line, in the recipient's language.</param>
/// <param name="TextBody">Plain-text body.</param>
/// <param name="HtmlBody">Optional HTML alternative.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);

/// <summary>
/// Sends transactional email (M2-5: invitations and password resets). Minimal on purpose: the Communications building
/// block (architecture §14) replaces it with templated, recorded communications later.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends <paramref name="message"/>; throws when the server refuses it.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>SMTP settings, section <c>Email</c>. Locally, Mailpit on port 1025 (no TLS, no credentials).</summary>
public sealed class SmtpEmailOptions
{
    /// <summary>Configuration section.</summary>
    public const string Section = "Email";

    /// <summary>Sender address.</summary>
    public string? From { get; set; }

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "Shelter";

    /// <summary>SMTP host.</summary>
    public string? Host { get; set; }

    /// <summary>SMTP port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Use STARTTLS.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>SMTP user, when the server requires one.</summary>
    public string? UserName { get; set; }

    /// <summary>SMTP password, when the server requires one. A secret: from the environment, never committed.</summary>
    public string? Password { get; set; }
}

/// <summary><see cref="IEmailSender"/> over SMTP (<see cref="SmtpClient"/>; no extra dependency).</summary>
public sealed class SmtpEmailSender(SmtpEmailOptions options) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.From))
        {
            throw new InvalidOperationException("Email:Host and Email:From must be configured to send email.");
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);
        if (message.HtmlBody is not null)
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));
        }

        using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
        if (!string.IsNullOrEmpty(options.UserName))
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>Registration of the email sender.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>Adds <see cref="SmtpEmailSender"/> from section <c>Email</c>, unless a sender is already registered.</summary>
    public static IServiceCollection AddShelterEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new SmtpEmailOptions();
        configuration.GetSection(SmtpEmailOptions.Section).Bind(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
