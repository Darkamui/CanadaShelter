using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Authorization;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Features.ReferenceData;

/// <summary>
/// <c>GET /api/animals/species</c>: the species list with both labels, the current tenant's overrides applied.
/// </summary>
internal static class ListSpeciesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/species", Handle)
            .WithName("ListAnimalsSpecies")
            .RequirePermission(AnimalPermissions.Read);

    internal static async Task<Ok<IReadOnlyList<ReferenceItem>>> Handle(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<Species>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<SpeciesOverride>().AsNoTracking().ToListAsync(cancellationToken);

        return TypedResults.Ok(ReferenceList.Merge(
            globals.Select(s => new ReferenceEntry(s.Code, s.Label, s.SortOrder)),
            overrides.Select(o => new ReferenceEntry(o.Code, o.Label, o.SortOrder, o.IsHidden))));
    }
}
