namespace Shelter.Modules.Animals.Contracts;

/// <summary>Which animals are where, for other modules (ADR 0021). Scoped, read-only, current organization only.</summary>
public interface IAnimalPopulation
{
    /// <summary>
    /// The number of animals in care at each of <paramref name="locationIds"/> (that location only, not below it).
    /// Locations without animals are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountAtAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken);
}
