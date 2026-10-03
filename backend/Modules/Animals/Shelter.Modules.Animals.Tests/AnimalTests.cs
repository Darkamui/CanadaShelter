using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;
using Shelter.Modules.Animals.Features.Animals;

namespace Shelter.Modules.Animals.Tests;

/// <summary>M3-4: animal, identifier and request validation rules that need no database.</summary>
public sealed class AnimalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateOnly Today = new(2026, 10, 2);

    [Fact]
    public void New_animal_is_not_in_care_and_searchable_by_its_unaccented_name()
    {
        var animal = new Animal(1, Details() with { Name = "Éclair" }, Now);

        Assert.Equal(AnimalCustodyState.NotInCare, animal.Custody);
        Assert.Equal("eclair", animal.SearchText);
    }

    [Fact]
    public void Animal_without_a_name_has_empty_search_text()
    {
        var animal = new Animal(1, Details() with { Name = null }, Now);

        Assert.Equal(string.Empty, animal.SearchText);
    }

    [Fact]
    public void Number_must_be_positive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Animal(0, Details(), Now));

    [Fact]
    public void Estimated_flag_is_dropped_without_a_birth_date()
    {
        var animal = new Animal(1, Details() with { BirthDate = null, BirthDateEstimated = true }, Now);

        Assert.False(animal.BirthDateEstimated);
    }

    [Fact]
    public void Update_leaves_the_custody_summary_alone()
    {
        var animal = new Animal(1, Details(), Now);
        var inCare = new AnimalCustodyState(CustodyStatuses.InCare, Guid.CreateVersion7(), Now, Guid.CreateVersion7(), null);
        animal.ApplyCustody(inCare, Now);

        animal.Update(Details() with { Name = "Autre nom" }, Now);

        Assert.Equal(inCare, animal.Custody);
        Assert.Equal("Autre nom", animal.Name);
    }

    [Fact]
    public void In_care_needs_a_location_and_only_in_care_has_one()
    {
        var animal = new Animal(1, Details(), Now);

        Assert.Throws<ArgumentException>(() => animal.ApplyCustody(new AnimalCustodyState(CustodyStatuses.InCare, null, Now, null, null), Now));
        Assert.Throws<ArgumentException>(() => animal.ApplyCustody(new AnimalCustodyState(CustodyStatuses.Outcome, Guid.CreateVersion7(), null, null, "adoption"), Now));
        Assert.Throws<ArgumentException>(() => animal.ApplyCustody(new AnimalCustodyState("lost", null, null, null, null), Now));
    }

    [Fact]
    public void Identifier_is_normalized_and_deactivated_once()
    {
        var identifier = new AnimalIdentifier(Guid.CreateVersion7(), AnimalIdentifierTypes.Microchip, " 900-123 456 ", Now);

        Assert.Equal("900-123 456", identifier.Value);
        Assert.Equal("900123456", identifier.NormalizedValue);
        Assert.True(identifier.IsActive);

        var first = Now.AddDays(1);
        identifier.Deactivate(first);
        identifier.Deactivate(first.AddDays(1));

        Assert.False(identifier.IsActive);
        Assert.Equal(first, identifier.DeactivatedAt);
    }

    [Fact]
    public void Identifier_needs_a_known_type_and_letters_or_digits()
    {
        Assert.Throws<ArgumentException>(() => new AnimalIdentifier(Guid.CreateVersion7(), "tattoo", "123", Now));
        Assert.Throws<ArgumentException>(() => new AnimalIdentifier(Guid.CreateVersion7(), AnimalIdentifierTypes.Licence, "- -", Now));
    }

    [Fact]
    public void Validator_trims_drops_blanks_and_defaults_unknowns()
    {
        var (details, errors) = AnimalRequestValidator.Validate(
            Request(name: "  Éclair ", speciesCode: " cat ", breed: "   "), Today);

        Assert.Empty(errors);
        Assert.Equal("Éclair", details!.Name);
        Assert.Equal("cat", details.SpeciesCode);
        Assert.Null(details.Breed);
        Assert.Equal(AnimalSexes.Unknown, details.Sex);
        Assert.Equal(ReproductiveStatuses.Unknown, details.ReproductiveStatus);
    }

    [Fact]
    public void Validator_reports_every_invalid_field()
    {
        var (details, errors) = AnimalRequestValidator.Validate(
            Request(
                name: new string('a', Animal.NameMaxLength + 1),
                sex: "other",
                reproductiveStatus: "spayed",
                birthDate: Today.AddDays(1),
                legalAlert: new string('a', Animal.NoteMaxLength + 1)),
            Today);

        Assert.Null(details);
        Assert.Equal(
            new HashSet<string> { "name", "speciesCode", "sex", "reproductiveStatus", "birthDate", "legalAlert" },
            errors.Keys.ToHashSet());
    }

    [Fact]
    public void Validator_refuses_birth_dates_before_1950() =>
        Assert.Contains(
            "birthDate",
            AnimalRequestValidator.Validate(Request(speciesCode: "dog", birthDate: new DateOnly(1949, 12, 31)), Today).Errors.Keys);

    private static CreateAnimalRequest Request(
        string? name = null,
        string? speciesCode = null,
        string? breed = null,
        string? sex = null,
        string? reproductiveStatus = null,
        DateOnly? birthDate = null,
        string? legalAlert = null) =>
        new(name, speciesCode, breed, null, null, sex, reproductiveStatus, birthDate, null, null, null, null, legalAlert, null);

    private static AnimalDetails Details() =>
        new("Éclair", "dog", null, null, null, AnimalSexes.Unknown, ReproductiveStatuses.Unknown, new DateOnly(2024, 5, 1), true, null, null, null, null);
}
