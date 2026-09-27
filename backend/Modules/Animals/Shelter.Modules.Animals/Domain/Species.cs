using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Animals.Domain;

/// <summary>A system species (reference data, ADR 0017): global, seeded by migrations, read-only at runtime. No personal data.</summary>
internal sealed class Species
{
    private Species()
    {
        Code = null!;
        Label = null!;
    }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }
}

/// <summary>
/// A tenant's change to the species list: relabels, reorders or hides the system species with the same code, or
/// adds a tenant species under a new code. No personal data.
/// </summary>
internal sealed class SpeciesOverride : ITenantOwned
{
    private SpeciesOverride()
    {
        Code = null!;
        Label = null!;
    }

    public SpeciesOverride(string code, LocalizedText label, int sortOrder, bool isHidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(label);

        Id = Guid.CreateVersion7();
        Code = code;
        Label = label;
        SortOrder = sortOrder;
        IsHidden = isHidden;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsHidden { get; private set; }
}
