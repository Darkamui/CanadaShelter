using System.Net.Mail;
using Shelter.Modules.People.Domain;

namespace Shelter.Modules.People.Features.People;

/// <summary>Checks a <see cref="PersonRequest"/> and turns it into <see cref="PersonDetails"/>: trimmed, blanks dropped.</summary>
internal static class PersonRequestValidator
{
    public const string French = "fr";
    public const string English = "en";

    /// <summary>The details, or the errors keyed by request field (English: the admin app maps them to catalog keys).</summary>
    public static (PersonDetails? Details, Dictionary<string, string[]> Errors) Validate(PersonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>();

        var firstName = Text(request.FirstName, "firstName", 100, errors);
        var lastName = Text(request.LastName, "lastName", 100, errors);
        var displayName = Text(request.DisplayName, "displayName", 200, errors);
        if (displayName is null && firstName is null && lastName is null && !errors.ContainsKey("displayName"))
        {
            errors["displayName"] = ["A display name or a first or last name is required."];
        }

        var email = Text(request.Email, "email", 256, errors);
        if (email is not null && (!MailAddress.TryCreate(email, out var address) || address.Address != email))
        {
            errors["email"] = ["A valid email address is required."];
        }

        var phone = Phone(request.Phone, "phone", errors);
        var secondaryPhone = Phone(request.SecondaryPhone, "secondaryPhone", errors);
        var addressLine = Text(request.AddressLine, "addressLine", 200, errors);
        var city = Text(request.City, "city", 100, errors);

        var province = Text(request.Province, "province", 2, errors)?.ToUpperInvariant();
        if (province is not null && !Provinces.All.Contains(province))
        {
            errors["province"] = ["A Canadian province or territory code, such as QC."];
        }

        var postalCode = Text(request.PostalCode, "postalCode", 10, errors)?.ToUpperInvariant();

        var language = request.PreferredLanguage?.Trim() ?? French;
        if (language is not (French or English))
        {
            errors["preferredLanguage"] = ["fr or en."];
        }

        var roleTags = request.RoleTags ?? [];
        if (roleTags.Any(tag => tag is null || !PersonRoles.All.Contains(tag)))
        {
            errors["roleTags"] = [$"Zero or more of: {string.Join(", ", PersonRoles.All.Order(StringComparer.Ordinal))}."];
        }

        var notes = Text(request.Notes, "notes", 4000, errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new PersonDetails(
            firstName, lastName, displayName, email, phone, secondaryPhone, addressLine, city, province, postalCode, language, roleTags, notes), errors);
    }

    private static string? Text(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors[field] = [$"At most {maxLength} characters."];
        }

        return trimmed;
    }

    private static string? Phone(string? value, string field, Dictionary<string, string[]> errors)
    {
        var phone = Text(value, field, 50, errors);
        if (phone is not null && PhoneNumbers.Key(phone) is null)
        {
            errors[field] = ["A phone number needs digits."];
        }

        return phone;
    }
}
