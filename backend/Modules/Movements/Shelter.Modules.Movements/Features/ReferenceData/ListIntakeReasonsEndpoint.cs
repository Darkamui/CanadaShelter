using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Movements.Authorization;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Features.ReferenceData;

/// <summary>
/// <c>GET /api/movements/intake-reasons</c>: the intake reason list with both labels, the current tenant's overrides applied.
/// </summary>
internal static class ListIntakeReasonsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/intake-reasons", Handle)
            .WithName("ListMovementsIntakeReasons")
            .RequirePermission(MovementPermissions.Read);

    internal static async Task<Ok<IReadOnlyList<ReferenceItem>>> Handle(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<IntakeReason>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<IntakeReasonOverride>().AsNoTracking().ToListAsync(cancellationToken);

        return TypedResults.Ok(ReferenceList.Merge(
            globals.Select(s => new ReferenceEntry(s.Code, s.Label, s.SortOrder)),
            overrides.Select(o => new ReferenceEntry(o.Code, o.Label, o.SortOrder, o.IsHidden))));
    }
}
