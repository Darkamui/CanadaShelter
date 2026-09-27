using Microsoft.Extensions.Compliance.Classification;

namespace Shelter.BuildingBlocks.Logging;

/// <summary>
/// Data classification taxonomy. Log redaction uses it today; audit classification
/// (crypto-shredding, architecture §17) reuses it in M1.
/// </summary>
public static class ShelterDataClassifications
{
    /// <summary>Taxonomy name.</summary>
    public const string TaxonomyName = "Shelter";

    /// <summary>Personal information (Law 25). Never written to logs or telemetry.</summary>
    public static DataClassification Personal { get; } = new(TaxonomyName, nameof(Personal));

    /// <summary>Explicitly non-personal; logged as-is.</summary>
    public static DataClassification NonPersonal { get; } = new(TaxonomyName, nameof(NonPersonal));
}

/// <summary>Marks a log parameter or property as personal information; its value is erased from logs.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>Creates the attribute.</summary>
    public PersonalDataAttribute()
        : base(ShelterDataClassifications.Personal)
    {
    }
}

/// <summary>Marks a log parameter or property as explicitly non-personal.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NonPersonalDataAttribute : DataClassificationAttribute
{
    /// <summary>Creates the attribute.</summary>
    public NonPersonalDataAttribute()
        : base(ShelterDataClassifications.NonPersonal)
    {
    }
}
