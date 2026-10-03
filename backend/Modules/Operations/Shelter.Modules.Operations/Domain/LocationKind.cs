using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Operations.Domain;

/// <summary>
/// A system location kind (reference data, ADR 0017): global, seeded by migrations, read-only at runtime. No personal
/// data. <see cref="HoldsAnimals"/> says whether animals can be placed at a location of this kind.
/// </summary>
internal sealed class LocationKind
{
    private LocationKind()
    {
        Code = null!;
        Label = null!;
    }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }

    public bool HoldsAnimals { get; private set; }
}

/// <summary>
/// A tenant's change to the location kind list: relabels, reorders or hides the system kind with the same code, or adds a
/// tenant kind under a new code. Like the labels, <see cref="HoldsAnimals"/> is always given and wins over the system
/// value (ADR 0017 amendment). No personal data.
/// </summary>
internal sealed class LocationKindOverride : ITenantOwned
{
    private LocationKindOverride()
    {
        Code = null!;
        Label = null!;
    }

    public LocationKindOverride(string code, LocalizedText label, int sortOrder, bool isHidden, bool holdsAnimals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(label);

        Id = Guid.CreateVersion7();
        Code = code;
        Label = label;
        SortOrder = sortOrder;
        IsHidden = isHidden;
        HoldsAnimals = holdsAnimals;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsHidden { get; private set; }

    public bool HoldsAnimals { get; private set; }
}
