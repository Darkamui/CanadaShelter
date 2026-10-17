using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;

namespace Shelter.Modules.Operations.Features.Locations;

/// <summary>
/// Raw-SQL tree operations on <c>operations.location</c>. They run in the request's tenant transaction, as the runtime
/// role, so RLS limits every walk to the current organization.
/// </summary>
internal static class LocationTree
{
    /// <summary>
    /// Serializes location tree writes of one organization until the transaction ends, so two concurrent moves cannot
    /// together form a cycle and two creates cannot both pass the sibling name check. Other organizations are not blocked.
    /// </summary>
    public static Task LockAsync(ShelterDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"operations.location:{tenantId:D}";
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    /// <summary>Whether <paramref name="locationId"/> is <paramref name="parentId"/> or one of its ancestors.</summary>
    public static Task<bool> IsSelfOrAncestorAsync(ShelterDbContext db, Guid locationId, Guid parentId, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<bool>($"""
            WITH RECURSIVE ancestors(id, parent_id) AS (
                SELECT id, parent_id FROM operations.location WHERE id = {parentId}
                UNION
                SELECT l.id, l.parent_id FROM operations.location l JOIN ancestors a ON l.id = a.parent_id
            )
            SELECT EXISTS (SELECT 1 FROM ancestors WHERE id = {locationId}) AS "Value"
            """).SingleAsync(cancellationToken);

    /// <summary><paramref name="rootId"/> and every location below it; empty when the root is not found.</summary>
    public static Task<List<Guid>> SubtreeIdsAsync(ShelterDbContext db, Guid rootId, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<Guid>($"""
            WITH RECURSIVE subtree(id) AS (
                SELECT id FROM operations.location WHERE id = {rootId}
                UNION
                SELECT l.id FROM operations.location l JOIN subtree s ON l.parent_id = s.id
            )
            SELECT id AS "Value" FROM subtree
            """).ToListAsync(cancellationToken);
}
