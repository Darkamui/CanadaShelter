using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.People.Domain;

/// <summary>
/// A person the organization deals with: owner, adopter, foster, volunteer, donor, finder or surrenderer
/// (product-spec §5.2). Tenant-owned; every descriptive field is personal data, audited under the person's own ID
/// (ADR 0011). Never deleted in M3: archiving hides it from pickers and lists. Values arrive validated
/// (<see cref="PersonDetails"/>); the search columns are derived here so they always match.
/// </summary>
internal sealed class Person : ITenantOwned
{
    private Person()
    {
        DisplayName = null!;
        PreferredLanguage = null!;
        SearchText = null!;
    }

    public Person(PersonDetails details, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        CreatedAt = now;
        Apply(details, now);
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    /// <summary>Name shown in the app: as given, or "first last".</summary>
    public string DisplayName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? SecondaryPhone { get; private set; }

    public string? AddressLine { get; private set; }

    public string? City { get; private set; }

    /// <summary>Two-letter Canadian province or territory code.</summary>
    public string? Province { get; private set; }

    public string? PostalCode { get; private set; }

    /// <summary><c>fr</c> or <c>en</c>: the language to write to this person in.</summary>
    public string PreferredLanguage { get; private set; }

    /// <summary><see cref="PersonRoles"/> codes, sorted and distinct.</summary>
    public string[] RoleTags { get; private set; } = [];

    public string? Notes { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Names and email, normalized (<see cref="SearchNormalizer.Text"/>): what the list search matches.</summary>
    public string SearchText { get; private set; }

    /// <summary>Lower-case email, for duplicate hints.</summary>
    public string? NormalizedEmail { get; private set; }

    /// <summary><see cref="PhoneNumbers.Key"/> of <see cref="Phone"/>.</summary>
    public string? PhoneDigits { get; private set; }

    /// <summary><see cref="PhoneNumbers.Key"/> of <see cref="SecondaryPhone"/>.</summary>
    public string? SecondaryPhoneDigits { get; private set; }

    /// <summary>Replaces every editable field.</summary>
    public void Update(PersonDetails details, DateTimeOffset now) => Apply(details, now);

    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        UpdatedAt = now;
    }

    public void Unarchive(DateTimeOffset now)
    {
        IsArchived = false;
        UpdatedAt = now;
    }

    [MemberNotNull(nameof(DisplayName), nameof(PreferredLanguage), nameof(SearchText))]
    private void Apply(PersonDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        FirstName = details.FirstName;
        LastName = details.LastName;
        DisplayName = details.DisplayName ?? string.Join(' ', new[] { details.FirstName, details.LastName }.Where(n => n is not null));
        if (DisplayName.Length == 0)
        {
            throw new ArgumentException("A person needs a display name or a first or last name.", nameof(details));
        }

        Email = details.Email;
        Phone = details.Phone;
        SecondaryPhone = details.SecondaryPhone;
        AddressLine = details.AddressLine;
        City = details.City;
        Province = details.Province;
        PostalCode = details.PostalCode;
        PreferredLanguage = details.PreferredLanguage;
        RoleTags = [.. details.RoleTags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Notes = details.Notes;
        UpdatedAt = now;

        SearchText = SearchNormalizer.Text(string.Join(' ', DisplayName, FirstName, LastName, Email));
        NormalizedEmail = Email?.ToLowerInvariant();
        PhoneDigits = PhoneNumbers.Key(Phone);
        SecondaryPhoneDigits = PhoneNumbers.Key(SecondaryPhone);
    }
}

/// <summary>The editable fields of a <see cref="Person"/>, trimmed, validated, blanks as <see langword="null"/>.</summary>
internal sealed record PersonDetails(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    string? AddressLine,
    string? City,
    string? Province,
    string? PostalCode,
    string PreferredLanguage,
    IReadOnlyList<string> RoleTags,
    string? Notes);

/// <summary>The roles a person can have with the organization. Codes are stable: the UI translates them.</summary>
internal static class PersonRoles
{
    public const string Owner = "owner";
    public const string Adopter = "adopter";
    public const string Foster = "foster";
    public const string Volunteer = "volunteer";
    public const string Donor = "donor";
    public const string Finder = "finder";
    public const string Surrenderer = "surrenderer";

    public static IReadOnlySet<string> All { get; } =
        FrozenSet.Create(StringComparer.Ordinal, Owner, Adopter, Foster, Volunteer, Donor, Finder, Surrenderer);
}

/// <summary>Canadian province and territory codes.</summary>
internal static class Provinces
{
    public static IReadOnlySet<string> All { get; } =
        FrozenSet.Create(StringComparer.Ordinal, "AB", "BC", "MB", "NB", "NL", "NS", "NT", "NU", "ON", "PE", "QC", "SK", "YT");
}

/// <summary>Phone number comparison.</summary>
internal static class PhoneNumbers
{
    /// <summary>
    /// The digits of <paramref name="phone"/>, without the North American country code, so "+1 514-555-0100" and
    /// "(514) 555-0100" compare equal. <see langword="null"/> without digits.
    /// </summary>
    public static string? Key(string? phone)
    {
        var digits = SearchNormalizer.Digits(phone);
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        return digits.Length == 0 ? null : digits;
    }
}
