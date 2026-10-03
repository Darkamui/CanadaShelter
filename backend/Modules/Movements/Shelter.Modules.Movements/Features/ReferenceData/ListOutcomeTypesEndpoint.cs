using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Movements.Authorization;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Features.ReferenceData;

/// <summary>
/// <c>GET /api/movements/outcome-types</c>: the outcome types the organization can pick, with both labels and whether
/// the outcome needs a person (adoption, return to owner).
/// </summary>
internal static class ListOutcomeTypesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/outcome-types", Handle)
            .WithName("ListMovementsOutcomeTypes")
            .RequirePermission(MovementPermissions.Read);

    internal static async Task<Ok<IReadOnlyList<OutcomeTypeItem>>> Handle(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var types = await ReasonCatalog.OutcomeTypesAsync(db, tenant, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<OutcomeTypeItem>>(
            [.. ReasonCatalog.Visible(types).Select(t => new OutcomeTypeItem(t.Code, t.Label, SacCategories.Outcome.RequiresPerson(t.SacCategory)))]);
    }
}

/// <summary>A pickable outcome type: code, both labels, and whether a person must be recorded with it.</summary>
internal sealed record OutcomeTypeItem(string Code, LocalizedText Label, bool RequiresPerson);
