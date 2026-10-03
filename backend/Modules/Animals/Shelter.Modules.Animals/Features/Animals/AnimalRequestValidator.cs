using Shelter.BuildingBlocks.Search;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Features.Animals;

/// <summary>The descriptive fields shared by <see cref="CreateAnimalRequest"/> and <see cref="UpdateAnimalRequest"/>.</summary>
internal interface IAnimalFields
{
    string? Name { get; }

    string? SpeciesCode { get; }

    string? Breed { get; }

    string? SecondaryBreed { get; }

    string? Colour { get; }

    string? Sex { get; }

    string? ReproductiveStatus { get; }

    DateOnly? BirthDate { get; }

    bool? BirthDateEstimated { get; }

    string? Marks { get; }

    string? BehaviourAlert { get; }

    string? MedicalAlert { get; }

    string? LegalAlert { get; }
}

/// <summary>
/// Checks the animal fields and turns them into <see cref="AnimalDetails"/>: trimmed, blanks dropped, sex and
/// reproductive status <c>unknown</c> when omitted. The species is checked against the list by the caller.
/// </summary>
internal static class AnimalRequestValidator
{
    private static readonly DateOnly EarliestBirthDate = new(1950, 1, 1);

    /// <summary>The details, or the errors keyed by request field (English: the admin app maps them to catalog keys).</summary>
    public static (AnimalDetails? Details, Dictionary<string, string[]> Errors) Validate(IAnimalFields request, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>();

        var name = Text(request.Name, "name", Animal.NameMaxLength, errors);

        var speciesCode = request.SpeciesCode?.Trim();
        if (string.IsNullOrEmpty(speciesCode))
        {
            errors["speciesCode"] = ["Species is required."];
        }

        var breed = Text(request.Breed, "breed", Animal.TextMaxLength, errors);
        var secondaryBreed = Text(request.SecondaryBreed, "secondaryBreed", Animal.TextMaxLength, errors);
        var colour = Text(request.Colour, "colour", Animal.TextMaxLength, errors);

        var sex = request.Sex?.Trim() is { Length: > 0 } s ? s : AnimalSexes.Unknown;
        if (!AnimalSexes.All.Contains(sex))
        {
            errors["sex"] = ["unknown, male or female."];
        }

        var reproductiveStatus = request.ReproductiveStatus?.Trim() is { Length: > 0 } r ? r : ReproductiveStatuses.Unknown;
        if (!ReproductiveStatuses.All.Contains(reproductiveStatus))
        {
            errors["reproductiveStatus"] = ["unknown, intact or sterilized."];
        }

        if (request.BirthDate is { } birthDate && (birthDate > today || birthDate < EarliestBirthDate))
        {
            errors["birthDate"] = ["The birth date must be between 1950 and today."];
        }

        var marks = Text(request.Marks, "marks", Animal.NoteMaxLength, errors);
        var behaviourAlert = Text(request.BehaviourAlert, "behaviourAlert", Animal.NoteMaxLength, errors);
        var medicalAlert = Text(request.MedicalAlert, "medicalAlert", Animal.NoteMaxLength, errors);
        var legalAlert = Text(request.LegalAlert, "legalAlert", Animal.NoteMaxLength, errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new AnimalDetails(
            name, speciesCode!, breed, secondaryBreed, colour, sex, reproductiveStatus, request.BirthDate,
            request.BirthDateEstimated ?? false, marks, behaviourAlert, medicalAlert, legalAlert), errors);
    }

    /// <summary>The trimmed identifier value, or an error under <paramref name="field"/>.</summary>
    public static string? Identifier(string? value, string field, Dictionary<string, string[]> errors)
    {
        var trimmed = Text(value, field, AnimalIdentifier.ValueMaxLength, errors);
        if (trimmed is null)
        {
            errors.TryAdd(field, ["A value is required."]);
            return null;
        }

        if (SearchNormalizer.Identifier(trimmed).Length == 0)
        {
            errors.TryAdd(field, ["An identifier needs letters or digits."]);
        }

        return trimmed;
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
}
