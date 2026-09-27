using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Movements.Domain;

/// <summary>A system intake reason (reference data, ADR 0017): global, seeded by migrations, read-only at runtime. No personal data.</summary>
internal sealed class IntakeReason
{
    private IntakeReason()
    {
        Code = null!;
        Label = null!;
    }

    public string Code { get; private set; }

    public LocalizedText Label { get; private set; }

    public int SortOrder { get; private set; }
}

/// <summary>
/// A tenant's change to the intake reason list: relabels, reorders or hides the system intake reason with the same code, or
/// adds a tenant intake reason under a new code. No personal data.
/// </summary>
internal sealed class IntakeReasonOverride : ITenantOwned
{
    private IntakeReasonOverride()
    {
        Code = null!;
        Label = null!;
    }

    public IntakeReasonOverride(string code, LocalizedText label, int sortOrder, bool isHidden)
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
