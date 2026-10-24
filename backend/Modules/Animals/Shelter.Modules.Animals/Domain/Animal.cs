using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Contracts;

namespace Shelter.Modules.Animals.Domain;

/// <summary>
/// An animal record (product-spec §7). Tenant-owned and never deleted. Its fields are classified non-personal: an animal
/// has no audit subject to shred for. The marks and alert texts are free text meant to describe the animal; the UI tells
/// staff not to put personal information in them, but nothing enforces it, so audit must keep their values out (see
/// <c>docs/modules/animals.md</c>, privacy notes). The custody summary is a
/// projection of the Movements ledger and changes only through <see cref="IAnimalCustody"/>; the animal endpoints never
/// write it. <see cref="Version"/> (PostgreSQL <c>xmin</c>) makes edits optimistic.
/// </summary>
internal sealed class Animal : ITenantOwned
{
    public const int NameMaxLength = 100;
    public const int TextMaxLength = 100;
    public const int NoteMaxLength = 2000;

    private Animal()
    {
        SpeciesCode = null!;
        Sex = null!;
        ReproductiveStatus = null!;
        CustodyStatus = null!;
        SearchText = null!;
    }

    public Animal(int number, AnimalDetails details, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

        Id = Guid.CreateVersion7();
        Number = number;
        CreatedAt = now;
        CustodyStatus = CustodyStatuses.NotInCare;
        Apply(details, now);
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Per-organization sequential number, from 1, without gaps. Never changes.</summary>
    public int Number { get; private set; }

    public string? Name { get; private set; }

    /// <summary>A code of the merged species list.</summary>
    public string SpeciesCode { get; private set; }

    public string? Breed { get; private set; }

    public string? SecondaryBreed { get; private set; }

    public string? Colour { get; private set; }

    /// <summary>One of <see cref="AnimalSexes"/>.</summary>
    public string Sex { get; private set; }

    /// <summary>One of <see cref="ReproductiveStatuses"/>.</summary>
    public string ReproductiveStatus { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>Whether <see cref="BirthDate"/> is an estimate.</summary>
    public bool BirthDateEstimated { get; private set; }

    /// <summary>Distinctive marks: scars, markings, tattoos.</summary>
    public string? Marks { get; private set; }

    public string? BehaviourAlert { get; private set; }

    public string? MedicalAlert { get; private set; }

    public string? LegalAlert { get; private set; }

    /// <summary>One of <see cref="CustodyStatuses"/>.</summary>
    public string CustodyStatus { get; private set; }

    public Guid? CurrentLocationId { get; private set; }

    public DateTimeOffset? InCareSince { get; private set; }

    public Guid? CurrentIntakeId { get; private set; }

    public string? LastOutcomeCode { get; private set; }

    /// <summary><see cref="SearchNormalizer.Text"/> of <see cref="Name"/>; empty without a name.</summary>
    public string SearchText { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Row version (PostgreSQL <c>xmin</c>), the concurrency token of edits.</summary>
    public uint Version { get; private set; }

    public AnimalCustodyState Custody => new(CustodyStatus, CurrentLocationId, InCareSince, CurrentIntakeId, LastOutcomeCode);

    /// <summary>Replaces every descriptive field. The custody summary is not touched.</summary>
    public void Update(AnimalDetails details, DateTimeOffset now) => Apply(details, now);

    /// <summary>Replaces the custody summary. Only <see cref="IAnimalCustody"/> calls this.</summary>
    public void ApplyCustody(AnimalCustodyState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!CustodyStatuses.All.Contains(state.Status))
        {
            throw new ArgumentException("Unknown custody status.", nameof(state));
        }

        if ((state.Status == CustodyStatuses.InCare) != (state.LocationId is not null))
        {
            throw new ArgumentException("An animal in care has a location, and only then.", nameof(state));
        }

        CustodyStatus = state.Status;
        CurrentLocationId = state.LocationId;
        InCareSince = state.InCareSince;
        CurrentIntakeId = state.CurrentIntakeId;
        LastOutcomeCode = state.LastOutcomeCode;
        UpdatedAt = now;
    }

    [MemberNotNull(nameof(SpeciesCode), nameof(Sex), nameof(ReproductiveStatus), nameof(SearchText))]
    private void Apply(AnimalDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.SpeciesCode);
        if (!AnimalSexes.All.Contains(details.Sex) || !ReproductiveStatuses.All.Contains(details.ReproductiveStatus))
        {
            throw new ArgumentException("Unknown sex or reproductive status.", nameof(details));
        }

        Name = details.Name;
        SpeciesCode = details.SpeciesCode;
        Breed = details.Breed;
        SecondaryBreed = details.SecondaryBreed;
        Colour = details.Colour;
        Sex = details.Sex;
        ReproductiveStatus = details.ReproductiveStatus;
        BirthDate = details.BirthDate;
        BirthDateEstimated = details.BirthDate is not null && details.BirthDateEstimated;
        Marks = details.Marks;
        BehaviourAlert = details.BehaviourAlert;
        MedicalAlert = details.MedicalAlert;
        LegalAlert = details.LegalAlert;
        SearchText = SearchNormalizer.Text(Name);
        UpdatedAt = now;
    }
}

/// <summary>The editable fields of an <see cref="Animal"/>, trimmed and validated, blanks as <see langword="null"/>.</summary>
internal sealed record AnimalDetails(
    string? Name,
    string SpeciesCode,
    string? Breed,
    string? SecondaryBreed,
    string? Colour,
    string Sex,
    string ReproductiveStatus,
    DateOnly? BirthDate,
    bool BirthDateEstimated,
    string? Marks,
    string? BehaviourAlert,
    string? MedicalAlert,
    string? LegalAlert);

/// <summary>Sex codes. Stable: clients translate them.</summary>
internal static class AnimalSexes
{
    public const string Unknown = "unknown";
    public const string Male = "male";
    public const string Female = "female";

    public static IReadOnlySet<string> All { get; } = FrozenSet.Create(StringComparer.Ordinal, Unknown, Male, Female);
}

/// <summary>Reproductive status codes. Stable: clients translate them.</summary>
internal static class ReproductiveStatuses
{
    public const string Unknown = "unknown";
    public const string Intact = "intact";
    public const string Sterilized = "sterilized";

    public static IReadOnlySet<string> All { get; } = FrozenSet.Create(StringComparer.Ordinal, Unknown, Intact, Sterilized);
}
