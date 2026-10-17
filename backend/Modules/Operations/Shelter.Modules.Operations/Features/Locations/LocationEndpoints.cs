using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Operations.Authorization;
using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Features.Locations;

/// <summary>
/// <c>/api/operations/locations</c> and <c>/api/operations/location-kinds</c> (M3-3). Every write takes the
/// organization's location lock, then checks the tree rules: the parent exists in the organization and is active, the
/// name is unique among active siblings, a move never makes a location its own ancestor, and a new kind is one the
/// organization can pick.
/// </summary>
internal static class LocationEndpoints
{
    /// <summary>The <c>code</c> of the 409 returned when archiving a location that still contains active ones.</summary>
    public const string HasActiveChildren = "location.hasActiveChildren";

    /// <summary>The <c>code</c> of the 409 returned when archiving a location where animals are in care.</summary>
    public const string HasAnimals = "location.hasAnimals";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/location-kinds", ListKinds)
            .WithName("ListLocationKinds")
            .RequirePermission(OperationsPermissions.LocationRead);

        endpoints.MapGet("/locations", List)
            .WithName("ListLocations")
            .RequirePermission(OperationsPermissions.LocationRead);

        endpoints.MapPost("/locations", Create)
            .WithName("CreateLocation")
            .RequirePermission(OperationsPermissions.LocationWrite);

        endpoints.MapPut("/locations/{locationId:guid}", Update)
            .WithName("UpdateLocation")
            .RequirePermission(OperationsPermissions.LocationWrite);

        endpoints.MapPost("/locations/{locationId:guid}/archive", Archive)
            .WithName("ArchiveLocation")
            .RequirePermission(OperationsPermissions.LocationWrite);
    }

    /// <summary>The kinds the organization can pick, with both labels, in list order.</summary>
    internal static async Task<Ok<IReadOnlyList<LocationKindItem>>> ListKinds(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var kinds = await LocationKindCatalog.LoadAsync(db, tenant, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<LocationKindItem>>(
            [.. LocationKindCatalog.Visible(kinds).Select(k => new LocationKindItem(k.Code, k.Label, k.HoldsAnimals))]);
    }

    /// <summary>
    /// The whole tree as a flat list in name order; clients nest it by <c>parentId</c>. Active locations only unless
    /// <c>includeArchived=true</c>.
    /// </summary>
    internal static async Task<Ok<IReadOnlyList<LocationItem>>> List(
        ShelterDbContext db, [FromQuery(Name = "includeArchived")] bool? includeArchived, CancellationToken cancellationToken)
    {
        var locations = db.Set<Location>().AsNoTracking();
        if (includeArchived != true)
        {
            locations = locations.Where(l => !l.IsArchived);
        }

        var items = await locations
            .OrderBy(l => l.NormalizedName)
            .ThenBy(l => l.Id)
            .Select(l => new LocationItem(l.Id, l.ParentId, l.KindCode, l.Name, l.Capacity, l.IsArchived))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<LocationItem>>(items);
    }

    internal static async Task<Results<Created<LocationItem>, ValidationProblem>> Create(
        LocationRequest request, ShelterDbContext db, ITenantContext tenant, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        await LocationTree.LockAsync(db, tenant.RequireTenantId(), cancellationToken);

        var errors = await ValidateAsync(request, existing: null, db, tenant, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var location = new Location(request.ParentId, request.KindCode!, request.Name!, request.Capacity, timeProvider.GetUtcNow());
        db.Set<Location>().Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/operations/locations/{location.Id:D}", LocationItem.From(location));
    }

    internal static async Task<Results<Ok<LocationItem>, NotFound, ValidationProblem>> Update(
        Guid locationId, LocationRequest request, ShelterDbContext db, ITenantContext tenant, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await LocationTree.LockAsync(db, tenant.RequireTenantId(), cancellationToken);

        var location = await db.Set<Location>().SingleOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        if (location is null)
        {
            return TypedResults.NotFound();
        }

        var errors = await ValidateAsync(request, location, db, tenant, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        location.Update(request.ParentId, request.KindCode!, request.Name!, request.Capacity, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(LocationItem.From(location));
    }

    /// <summary>
    /// Archives a location. Its active children must be archived or moved first, and its animals moved out (409).
    /// </summary>
    internal static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Archive(
        Guid locationId,
        ShelterDbContext db,
        ITenantContext tenant,
        IAnimalPopulation population,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await LocationTree.LockAsync(db, tenant.RequireTenantId(), cancellationToken);

        // Waits for movements placing an animal here (ILocationDirectory.LockForPlacementAsync) to commit, so the count
        // below sees them, and blocks new ones until the archive commits.
        await db.Database
            .SqlQuery<Guid>($"""SELECT id AS "Value" FROM operations.location WHERE id = {locationId} FOR UPDATE""")
            .ToListAsync(cancellationToken);

        var location = await db.Set<Location>().SingleOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        if (location is null)
        {
            return TypedResults.NotFound();
        }

        if (location.IsArchived)
        {
            return TypedResults.NoContent();
        }

        if (await db.Set<Location>().AnyAsync(l => l.ParentId == locationId && !l.IsArchived, cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Archive or move the locations inside this one first.",
                extensions: new Dictionary<string, object?> { ["code"] = HasActiveChildren });
        }

        if ((await population.CountAtAsync([locationId], cancellationToken)).Count > 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Move the animals at this location elsewhere first.",
                extensions: new Dictionary<string, object?> { ["code"] = HasAnimals });
        }

        location.Archive(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Field errors for a create (<paramref name="existing"/> null) or an update. Call under the tree lock.</summary>
    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        LocationRequest request, Location? existing, ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > Location.NameMaxLength)
        {
            errors["name"] = [$"Name must be at most {Location.NameMaxLength} characters."];
        }

        if (request.Capacity is < 0 or > Location.MaxCapacity)
        {
            errors["capacity"] = [$"Capacity must be between 0 and {Location.MaxCapacity}."];
        }

        // A kind that was hidden after the location got it stays valid; a new choice must be a visible kind.
        if (string.IsNullOrWhiteSpace(request.KindCode))
        {
            errors["kindCode"] = ["Kind is required."];
        }
        else if (request.KindCode != existing?.KindCode)
        {
            var kinds = await LocationKindCatalog.LoadAsync(db, tenant, cancellationToken);
            if (!kinds.TryGetValue(request.KindCode, out var kind) || kind.IsHidden)
            {
                errors["kindCode"] = ["Unknown location kind."];
            }
        }

        if (request.ParentId is { } parentId)
        {
            var parent = await db.Set<Location>().AsNoTracking()
                .Where(l => l.Id == parentId)
                .Select(l => new { l.IsArchived })
                .SingleOrDefaultAsync(cancellationToken);
            if (parent is null)
            {
                errors["parentId"] = ["Parent location not found."];
            }
            else if (parent.IsArchived)
            {
                errors["parentId"] = ["The parent location is archived."];
            }
            else if (existing is not null && await LocationTree.IsSelfOrAncestorAsync(db, existing.Id, parentId, cancellationToken))
            {
                errors["parentId"] = ["A location cannot be moved inside itself."];
            }
        }

        if (!errors.ContainsKey("name") && !errors.ContainsKey("parentId") && name is not null && (existing is null || !existing.IsArchived))
        {
            var normalized = SearchNormalizer.Text(name);
            var existingId = existing?.Id;
            var taken = await db.Set<Location>().AnyAsync(
                l => l.ParentId == request.ParentId && l.NormalizedName == normalized && !l.IsArchived && l.Id != existingId,
                cancellationToken);
            if (taken)
            {
                errors["name"] = ["Another active location here already has this name."];
            }
        }

        return errors;
    }
}

/// <summary>Create or update a location. <c>parentId</c> null puts it at the top of the tree.</summary>
internal sealed record LocationRequest(Guid? ParentId, string? KindCode, string? Name, int? Capacity);

/// <summary>A location as the tree returns it.</summary>
internal sealed record LocationItem(Guid Id, Guid? ParentId, string KindCode, string Name, int? Capacity, bool IsArchived)
{
    public static LocationItem From(Location l) => new(l.Id, l.ParentId, l.KindCode, l.Name, l.Capacity, l.IsArchived);
}

/// <summary>A pickable location kind: code, both labels, and whether animals can be placed there.</summary>
internal sealed record LocationKindItem(string Code, LocalizedText Label, bool HoldsAnimals);
