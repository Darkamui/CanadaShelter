using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Movements.Domain;

/// <summary>
/// A system outcome type (reference data, ADR 0017): global, seeded by migrations, read-only at runtime. No personal
/// data. <see cref="SacCategory"/> is one of <see cref="SacCategories.Outcome"/>; it decides whether a person is
/// required and whether the animal can come back.
/// </summary>
internal sealed class OutcomeType
{
    private OutcomeType()
    {
        Code = null!;
        Label = null!;
        SacCategory = null!;
    }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }

    public string SacCategory { get; private set; }
}

/// <summary>
/// A tenant's change to the outcome type list: relabels, reorders or hides the system outcome type with the same code,
/// or adds a tenant outcome type under a new code. <see cref="SacCategory"/> is always given and wins over the system
/// value (ADR 0017 amendment 1). No personal data.
/// </summary>
internal sealed class OutcomeTypeOverride : ITenantOwned
{
    private OutcomeTypeOverride()
    {
        Code = null!;
        Label = null!;
        SacCategory = null!;
    }

    public OutcomeTypeOverride(string code, LocalizedText label, int sortOrder, bool isHidden, string sacCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(label);
        if (!SacCategories.Outcome.All.Contains(sacCategory))
        {
            throw new ArgumentException("Unknown SAC outcome category.", nameof(sacCategory));
        }

        Id = Guid.CreateVersion7();
        Code = code;
        Label = label;
        SortOrder = sortOrder;
        IsHidden = isHidden;
        SacCategory = sacCategory;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsHidden { get; private set; }

    public string SacCategory { get; private set; }
}
