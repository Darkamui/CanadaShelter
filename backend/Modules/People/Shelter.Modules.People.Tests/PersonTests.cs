using Shelter.Modules.People.Domain;
using Shelter.Modules.People.Features.People;

namespace Shelter.Modules.People.Tests;

/// <summary>M3-2: the person's derived columns and request validation, without a database.</summary>
public sealed class PersonTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Display_name_defaults_to_first_and_last_name()
    {
        var person = new Person(Details(firstName: "Élodie", lastName: "Gagnon"), Now);

        Assert.Equal("Élodie Gagnon", person.DisplayName);
    }

    [Fact]
    public void Search_columns_are_normalized()
    {
        var person = new Person(
            Details(firstName: "Éclair", lastName: "Côté", email: "Eclair.Cote@Exemple.CA", phone: "+1 (514) 555-0100", secondaryPhone: "450 555 0199"),
            Now);

        Assert.Contains("eclair", person.SearchText, StringComparison.Ordinal);
        Assert.Contains("cote", person.SearchText, StringComparison.Ordinal);
        Assert.Equal("eclair.cote@exemple.ca", person.NormalizedEmail);
        Assert.Equal("5145550100", person.PhoneDigits);
        Assert.Equal("4505550199", person.SecondaryPhoneDigits);
    }

    [Fact]
    public void Role_tags_are_distinct_and_sorted()
    {
        var person = new Person(Details(firstName: "A", roleTags: [PersonRoles.Volunteer, PersonRoles.Adopter, PersonRoles.Volunteer]), Now);

        Assert.Equal([PersonRoles.Adopter, PersonRoles.Volunteer], person.RoleTags);
    }

    [Fact]
    public void Archive_and_unarchive_set_the_flag_and_date()
    {
        var person = new Person(Details(firstName: "A"), Now);

        person.Archive(Now.AddDays(1));
        Assert.True(person.IsArchived);
        Assert.Equal(Now.AddDays(1), person.UpdatedAt);

        person.Unarchive(Now.AddDays(2));
        Assert.False(person.IsArchived);
    }

    [Fact]
    public void A_person_without_any_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Person(Details(), Now));
    }

    [Theory]
    [InlineData("(514) 555-0100", "5145550100")]
    [InlineData("1-514-555-0100", "5145550100")]
    [InlineData("555-0100", "5550100")]
    [InlineData("poste", null)]
    public void Phone_key_drops_formatting_and_country_code(string phone, string? expected)
    {
        Assert.Equal(expected, PhoneNumbers.Key(phone));
    }

    [Fact]
    public void Validation_trims_values_and_drops_blanks()
    {
        var (details, errors) = PersonRequestValidator.Validate(
            Request(firstName: "  Élodie ", lastName: " ", province: "qc", postalCode: "h2x 1y4"));

        Assert.Empty(errors);
        Assert.NotNull(details);
        Assert.Equal("Élodie", details.FirstName);
        Assert.Null(details.LastName);
        Assert.Equal("QC", details.Province);
        Assert.Equal("H2X 1Y4", details.PostalCode);
        Assert.Equal("fr", details.PreferredLanguage);
    }

    [Fact]
    public void Validation_reports_each_invalid_field()
    {
        var (details, errors) = PersonRequestValidator.Validate(
            Request(email: "pas-un-courriel", phone: "aucun", province: "ZZ", language: "de", roleTags: ["owner", "boss"]));

        Assert.Null(details);
        Assert.Equal(
            ["displayName", "email", "phone", "preferredLanguage", "province", "roleTags"],
            errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Validation_limits_lengths()
    {
        var (_, errors) = PersonRequestValidator.Validate(Request(firstName: new string('a', 101)));

        Assert.Equal(["firstName"], errors.Keys);
    }

    private static PersonDetails Details(
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? phone = null,
        string? secondaryPhone = null,
        IReadOnlyList<string>? roleTags = null) =>
        new(firstName, lastName, null, email, phone, secondaryPhone, null, null, null, null, "fr", roleTags ?? [], null);

    private static PersonRequest Request(
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        string? phone = null,
        string? province = null,
        string? postalCode = null,
        string? language = null,
        IReadOnlyList<string>? roleTags = null) =>
        new(firstName, lastName, null, email, phone, null, null, null, province, postalCode, language, roleTags, null);
}
