using System.Collections.Frozen;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Animals.Domain;

/// <summary>
/// A microchip, licence or external number of an animal. Tenant-owned, not personal data. Never deleted: a wrong or
/// replaced identifier is deactivated. An active microchip is unique in the organization (unique partial index on the
/// normalized value).
/// </summary>
internal sealed class AnimalIdentifier : ITenantOwned
{
    public const int ValueMaxLength = 50;

    private AnimalIdentifier()
    {
        Type = null!;
        Value = null!;
        NormalizedValue = null!;
    }

    public AnimalIdentifier(Guid animalId, string type, string value, DateTimeOffset now)
    {
        if (!AnimalIdentifierTypes.All.Contains(type))
        {
            throw new ArgumentException("Unknown identifier type.", nameof(type));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = SearchNormalizer.Identifier(value);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("An identifier needs letters or digits.", nameof(value));
        }

        Id = Guid.CreateVersion7();
        AnimalId = animalId;
        Type = type;
        Value = value.Trim();
        NormalizedValue = normalized;
        IsActive = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public Guid AnimalId { get; private set; }

    /// <summary>One of <see cref="AnimalIdentifierTypes"/>.</summary>
    public string Type { get; private set; }

    /// <summary>As entered, trimmed.</summary>
    public string Value { get; private set; }

    /// <summary><see cref="SearchNormalizer.Identifier"/> of <see cref="Value"/>: uniqueness and search use it.</summary>
    public string NormalizedValue { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAt = now;
    }
}

/// <summary>Identifier type codes. Stable: clients translate them.</summary>
internal static class AnimalIdentifierTypes
{
    public const string Microchip = "microchip";
    public const string Licence = "licence";
    public const string External = "external";

    public static IReadOnlySet<string> All { get; } = FrozenSet.Create(StringComparer.Ordinal, Microchip, Licence, External);
}
