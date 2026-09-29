using Microsoft.AspNetCore.Identity;

namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// A person's sign-in account (architecture §9.1, ADR 0018). A <b>global</b> row: one account, many organizations
/// through staff memberships. It grants nothing by itself; access comes from a membership, never from the email.
/// Every column is classified; accounts are outside the tenant audit, and security activity goes to
/// <see cref="SecurityEvent"/>.
/// </summary>
internal sealed class UserAccount : IdentityUser<Guid>
{
    private UserAccount()
    {
        DisplayName = null!;
        PreferredLanguage = null!;
    }

    public UserAccount(string email, string displayName, string preferredLanguage, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = Guid.CreateVersion7();
        UserName = email;
        Email = email;
        DisplayName = displayName;
        PreferredLanguage = Languages.Normalize(preferredLanguage);
        CreatedAt = createdAt;
        LockoutEnabled = true;
    }

    /// <summary>How the person is named in the staff app.</summary>
    public string DisplayName { get; private set; }

    /// <summary><c>fr</c> or <c>en</c>: the language of emails sent to this account (architecture §11.3).</summary>
    public string PreferredLanguage { get; private set; }

    /// <summary>
    /// Platform operator (SaaS staff): may open the job dashboard. Set only by SQL or provisioning, never through
    /// the API; MFA is mandatory (ADR 0018).
    /// </summary>
    public bool IsPlatformOperator { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>The two supported communication languages (architecture §11.1).</summary>
internal static class Languages
{
    public const string French = "fr";
    public const string English = "en";

    /// <summary><c>en</c> when asked for English, otherwise French (the default).</summary>
    public static string Normalize(string? language) =>
        string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : French;
}
