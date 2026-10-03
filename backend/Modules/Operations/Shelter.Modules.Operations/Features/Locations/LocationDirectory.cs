using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Features.Locations;

/// <summary><see cref="ILocationDirectory"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021). Read-only, apart from the placement lock.</summary>
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

    public async Task<bool> LockForPlacementAsync(Guid id, CancellationToken cancellationToken)
    {
        // The lock waits for a concurrent archive (FOR UPDATE in LocationEndpoints.Archive); the WHERE is then
        // re-checked against the archived row, so a location archived meanwhile is not found.
        var kindCode = (await db.Database
            .SqlQuery<string>($"""SELECT kind_code AS "Value" FROM operations.location WHERE id = {id} AND NOT is_archived FOR SHARE""")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();
        if (kindCode is null)
        {
            return false;
        }

        // Hidden kinds keep their attribute: hiding only removes the kind from new choices.
        var kinds = await LocationKindCatalog.LoadAsync(db, tenant, cancellationToken);
        return kinds.TryGetValue(kindCode, out var kind) && kind.HoldsAnimals;
    }
}
