using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Movements.Domain;

/// <summary>
/// A system intake reason (reference data, ADR 0017): global, seeded by migrations, read-only at runtime. No personal
/// data. <see cref="SacCategory"/> is one of <see cref="SacCategories.Intake"/>.
/// </summary>
internal sealed class IntakeReason
{
    private IntakeReason()
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
/// A tenant's change to the intake reason list: relabels, reorders or hides the system intake reason with the same code, or
/// adds a tenant intake reason under a new code. Like the labels, <see cref="SacCategory"/> is always given and wins over
/// the system value (ADR 0017 amendment 1). No personal data.
/// </summary>
internal sealed class IntakeReasonOverride : ITenantOwned
{
    private IntakeReasonOverride()
    {
        Code = null!;
        Label = null!;
        SacCategory = null!;
    }

    public IntakeReasonOverride(string code, LocalizedText label, int sortOrder, bool isHidden, string sacCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(label);
        if (!SacCategories.Intake.All.Contains(sacCategory))
        {
            throw new ArgumentException("Unknown SAC intake category.", nameof(sacCategory));
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
