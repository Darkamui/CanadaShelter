using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Features.Locations;

/// <summary><see cref="ILocationDirectory"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021). Read-only.</summary>
internal sealed class LocationDirectory(ShelterDbContext db, ITenantContext tenant) : ILocationDirectory
{
    public async Task<IReadOnlyDictionary<Guid, LocationSummary>> GetAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, LocationSummary>();
        }

        List<Guid> distinct = [.. ids.Distinct()];
        return await db.Set<Location>()
            .AsNoTracking()
            .Where(l => distinct.Contains(l.Id))
            .Select(l => new LocationSummary(l.Id, l.ParentId, l.Name, l.KindCode, l.IsArchived))
            .ToDictionaryAsync(l => l.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid rootId, CancellationToken cancellationToken) =>
        await LocationTree.SubtreeIdsAsync(db, rootId, cancellationToken);

    public async Task<bool> IsActiveHoldingAsync(Guid id, CancellationToken cancellationToken)
    {
        var kindCode = await db.Set<Location>()
            .Where(l => l.Id == id && !l.IsArchived)
            .Select(l => l.KindCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (kindCode is null)
        {
            return false;
        }

        // Hidden kinds keep their attribute: hiding only removes the kind from new choices.
        var kinds = await LocationKindCatalog.LoadAsync(db, tenant, cancellationToken);
        return kinds.TryGetValue(kindCode, out var kind) && kind.HoldsAnimals;
    }
}
