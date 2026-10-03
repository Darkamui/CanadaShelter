namespace Shelter.Modules.Operations.Contracts;

/// <summary>
/// Read access to the current organization's locations for other modules (ADR 0021). Scoped: it shares the caller's
/// <c>ShelterDbContext</c> and transaction. IDs of another organization's locations are never found.
/// </summary>
public interface ILocationDirectory
{
    /// <summary>The locations among <paramref name="ids"/> that exist, archived ones included, keyed by ID.</summary>
    Task<IReadOnlyDictionary<Guid, LocationSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="rootId"/> and every location below it, archived ones included; empty when the root does not exist.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid rootId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the location exists, is not archived and is of a kind that holds animals. When it is, the location row
    /// stays share-locked (<c>SELECT … FOR SHARE</c>) until the transaction ends, so it cannot be archived while the
    /// caller places an animal there.
    /// </summary>
    Task<bool> LockForPlacementAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>What another module needs to show a location.</summary>
/// <param name="Id">Location ID.</param>
/// <param name="ParentId">Containing location, or <see langword="null"/> at the top.</param>
/// <param name="Name">Plain-text name.</param>
/// <param name="KindCode">Code of the location kind list.</param>
/// <param name="IsArchived">Whether the location is archived.</param>
public sealed record LocationSummary(Guid Id, Guid? ParentId, string Name, string KindCode, bool IsArchived);
