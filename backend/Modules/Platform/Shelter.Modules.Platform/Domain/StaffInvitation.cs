using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// An invitation to join the organization as staff, sent by email (M2-5, ADR 0018). Tenant-owned; the email is
/// personal data. Accepting it creates the <see cref="StaffMembership"/> with its roles: the email only says where the
/// secret was sent, it grants nothing by itself. The secret lives only as a hash in the global
/// <see cref="InvitationToken"/> table, so an anonymous accept finds the organization from the secret alone.
/// </summary>
internal sealed class StaffInvitation : ITenantOwned
{
    /// <summary>How long an invitation (and each resent link) stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private StaffInvitation()
    {
        Email = null!;
        NormalizedEmail = null!;
        Language = null!;
    }

    public StaffInvitation(string email, string normalizedEmail, IEnumerable<string> roleKeys, string language, Guid invitedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentNullException.ThrowIfNull(roleKeys);

        var keys = roleKeys.ToList();
        if (!StaffMembership.AreValid(keys))
        {
            throw new ArgumentException("Roles must be one or more system role keys.", nameof(roleKeys));
        }

        Id = Guid.CreateVersion7();
        Email = email;
        NormalizedEmail = normalizedEmail;
        RoleKeys = [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Language = Languages.Normalize(language);
        InvitedBy = invitedBy;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Where the invitation was sent. Personal data.</summary>
    public string Email { get; private set; }

    /// <summary><see cref="Email"/> as Identity normalizes it, to match an existing account and to find duplicates.</summary>
    public string NormalizedEmail { get; private set; }

    /// <summary>System role keys the membership gets, sorted and distinct.</summary>
    public string[] RoleKeys { get; private set; } = [];

    /// <summary><c>fr</c> or <c>en</c>: the email's language, and a new account's preferred language.</summary>
    public string Language { get; private set; }

    /// <summary>The account that sent (or last resent) it.</summary>
    public Guid InvitedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Neither accepted nor revoked (it may have expired).</summary>
    public bool IsOpen => AcceptedAt is null && RevokedAt is null;

    /// <summary>Open and not expired: its link can be accepted.</summary>
    public bool IsPending(DateTimeOffset now) => IsOpen && now < ExpiresAt;

    /// <summary>Extends the validity for a new link (the previous link's token is deleted by the caller).</summary>
    public void Renew(Guid invitedBy, DateTimeOffset now)
    {
        EnsureOpen();
        InvitedBy = invitedBy;
        ExpiresAt = now + Lifetime;
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsureOpen();
        RevokedAt = now;
    }

    public void Accept(DateTimeOffset now)
    {
        if (!IsPending(now))
        {
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        }

        AcceptedAt = now;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The invitation is already accepted or revoked.");
        }
    }
}

/// <summary>
/// Global lookup from an invitation secret to its organization (ADR 0018): an anonymous accept knows only the secret,
/// and needs the tenant before it can read the tenant-owned <see cref="StaffInvitation"/>. Holds no personal data:
/// a SHA-256 hash, two IDs and a date. Deleted when the invitation is accepted, revoked or resent (single use).
/// </summary>
internal sealed class InvitationToken
{
    private InvitationToken()
    {
        TokenHash = null!;
    }

    public InvitationToken(byte[] tokenHash, Guid organizationId, Guid invitationId, DateTimeOffset expiresAt)
    {
        TokenHash = tokenHash;
        OrganizationId = organizationId;
        InvitationId = invitationId;
        ExpiresAt = expiresAt;
    }

    /// <summary>SHA-256 of the secret's 32 bytes. The secret itself is only in the email.</summary>
    public byte[] TokenHash { get; private set; }

    /// <summary>The invitation's tenant. Not named <c>TenantId</c>: this table is global, not tenant-owned.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid InvitationId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
}

/// <summary>Invitation secrets: 32 random bytes, base64url in the link, SHA-256 at rest.</summary>
internal static class InvitationSecret
{
    private const int Length = 32;

    /// <summary>A new secret for the link, and its hash to store.</summary>
    public static (string Token, byte[] Hash) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(Length);
        return (WebEncoders.Base64UrlEncode(bytes), SHA256.HashData(bytes));
    }

    /// <summary>The hash of a secret from a link, or <see langword="null"/> when it is not one.</summary>
    public static byte[]? Hash(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = WebEncoders.Base64UrlDecode(token.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        return bytes.Length == Length ? SHA256.HashData(bytes) : null;
    }
}
