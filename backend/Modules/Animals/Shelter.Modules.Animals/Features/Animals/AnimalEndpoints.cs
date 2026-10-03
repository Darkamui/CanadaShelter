using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Persistence.Paging;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Authorization;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;
using Shelter.Modules.Animals.Features.ReferenceData;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.People.Contracts;

namespace Shelter.Modules.Animals.Features.Animals;

/// <summary>
/// <c>/api/animals</c>: animal records, identifiers and timeline (M3-4). Numbers come from a per-organization counter
/// incremented in the creating transaction, so they have no gaps. Edits are optimistic (<c>version</c>). The custody
/// summary is read-only here: only the Movements module changes it, through <see cref="IAnimalCustody"/>.
/// </summary>
internal static class AnimalEndpoints
{
    /// <summary>The <c>code</c> of the 409 returned when the animal changed since the client read it.</summary>
    public const string VersionConflict = "animal.versionConflict";

    private const string ActiveMicrochipIndex = "ux_animal_identifier_active_microchip";
    private const string SourceModule = "animals";
    private const string MicrochipTakenMessage = "Another animal already has this microchip.";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", List)
            .WithName("ListAnimals")
            .RequirePermission(AnimalPermissions.Read);

        endpoints.MapGet("/{animalId:guid}", Get)
            .WithName("GetAnimal")
            .RequirePermission(AnimalPermissions.Read);

        endpoints.MapGet("/{animalId:guid}/timeline", Timeline)
            .WithName("GetAnimalTimeline")
            .RequirePermission(AnimalPermissions.Read);

        endpoints.MapPost("", Create)
            .WithName("CreateAnimal")
            .RequirePermission(AnimalPermissions.Write);

        endpoints.MapPut("/{animalId:guid}", Update)
            .WithName("UpdateAnimal")
            .RequirePermission(AnimalPermissions.Write);

        endpoints.MapPost("/{animalId:guid}/identifiers", AddIdentifier)
            .WithName("AddAnimalIdentifier")
            .RequirePermission(AnimalPermissions.Write);

        endpoints.MapPost("/{animalId:guid}/identifiers/{identifierId:guid}/deactivate", DeactivateIdentifier)
            .WithName("DeactivateAnimalIdentifier")
            .RequirePermission(AnimalPermissions.Write);
    }

    /// <summary>
    /// A page of animals. <c>q</c> matches the name in part (without regard to accents or case), the number (with or
    /// without <c>#</c>) or an active identifier exactly. <c>locationId</c> includes the locations below it.
    /// </summary>
    internal static async Task<Results<Ok<PagedResult<AnimalListItem>>, ValidationProblem>> List(
        [AsParameters] ListAnimalsQuery query, ShelterDbContext db, ILocationDirectory locations, CancellationToken cancellationToken)
    {
        if (query.Status is not null && !CustodyStatuses.All.Contains(query.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
        }

        var animals = db.Set<Animal>().AsNoTracking();
        if (query.Status is not null)
        {
            animals = animals.Where(a => a.CustodyStatus == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Species))
        {
            animals = animals.Where(a => a.SpeciesCode == query.Species);
        }

        if (query.LocationId is { } locationId)
        {
            List<Guid?> subtree = [.. (await locations.GetSubtreeIdsAsync(locationId, cancellationToken)).Select(id => (Guid?)id)];
            animals = animals.Where(a => subtree.Contains(a.CurrentLocationId));
        }

        animals = Search(animals, db, query.Q);

        IOrderedQueryable<Animal> ordered = query.Sort switch
        {
            "number" => animals.OrderBy(a => a.Number),
            "name" => animals.OrderBy(a => a.SearchText).ThenBy(a => a.Number),
            "-name" => animals.OrderByDescending(a => a.SearchText).ThenByDescending(a => a.Number),
            _ => animals.OrderByDescending(a => a.Number),
        };

        var page = await ordered.ToPagedResultAsync(
            a => new AnimalListItem(
                a.Id, a.Number, a.Name, a.SpeciesCode, a.Breed, a.Sex, a.CustodyStatus, a.CurrentLocationId, null, a.InCareSince,
                a.BehaviourAlert != null || a.MedicalAlert != null || a.LegalAlert != null),
            new PageRequest(query.Page, query.PageSize),
            cancellationToken);

        var names = await LocationNamesAsync(locations, page.Items.Select(a => a.CurrentLocationId), cancellationToken);
        return TypedResults.Ok(page with
        {
            Items = [.. page.Items.Select(a => a with { CurrentLocationName = NameOf(names, a.CurrentLocationId) })],
        });
    }

    internal static async Task<Results<Ok<AnimalResponse>, NotFound>> Get(
        Guid animalId, ShelterDbContext db, ILocationDirectory locations, CancellationToken cancellationToken)
    {
        var animal = await db.Set<Animal>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == animalId, cancellationToken);
        return animal is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(await ResponseAsync(animal, db, locations, cancellationToken));
    }

    /// <summary>
    /// A page of the animal's timeline, newest first. Each event's <c>names</c> resolves its location and person IDs; the
    /// stored event holds IDs only. Without <c>person.read</c>, person IDs and names are left out.
    /// </summary>
    internal static async Task<Results<Ok<PagedResult<TimelineItem>>, NotFound>> Timeline(
        Guid animalId,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "pageSize")] int? pageSize,
        ShelterDbContext db,
        ILocationDirectory locations,
        IPersonDirectory people,
        IPermissionContext permissions,
        CancellationToken cancellationToken)
    {
        if (!await db.Set<Animal>().AnyAsync(a => a.Id == animalId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var rows = await db.Set<TimelineEvent>()
            .AsNoTracking()
            .Where(e => e.AnimalId == animalId)
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .ToPagedResultAsync(new PageRequest(page, pageSize), cancellationToken);

        var events = rows.Items
            .Select(e => (Event: e, Parameters: JsonSerializer.Deserialize<Dictionary<string, string>>(e.Parameters) ?? []))
            .ToList();

        // Without person.read, person IDs are dropped too: an ID linked to an event still reveals a relationship.
        var canReadPeople = permissions.Has(PeoplePermissionNames.Read);
        if (!canReadPeople)
        {
            events = [.. events.Select(e => (e.Event, Parameters: e.Parameters
                .Where(p => !p.Key.EndsWith("PersonId", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p => p.Key, p => p.Value)))];
        }

        var locationIds = IdsEndingWith(events.Select(e => e.Parameters), "LocationId");
        var personIds = canReadPeople ? IdsEndingWith(events.Select(e => e.Parameters), "PersonId") : [];
        var locationNames = await LocationNamesAsync(locations, locationIds.Select(id => (Guid?)id), cancellationToken);
        var personNames = personIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await people.GetSummariesAsync(personIds, cancellationToken)).ToDictionary(p => p.Key, p => p.Value.DisplayName);

        List<TimelineItem> items = [.. events.Select(e => new TimelineItem(
            e.Event.Id, e.Event.Type, e.Event.OccurredAt, e.Event.RecordedAt, e.Event.SourceModule, e.Event.SourceRecordId,
            e.Parameters, ResolveNames(e.Parameters, locationNames, personNames)))];
        return TypedResults.Ok(new PagedResult<TimelineItem>(items, rows.Page, rows.PageSize, rows.TotalCount));
    }

    /// <summary>
    /// Registers an animal: next number, optional microchip, and an <c>animal_registered</c> timeline event, together.
    /// The animal starts not in care; an intake (Movements) takes it in.
    /// </summary>
    internal static async Task<Results<Created<AnimalResponse>, ValidationProblem>> Create(
        CreateAnimalRequest request,
        ShelterDbContext db,
        ITenantContext tenant,
        IAnimalTimeline timeline,
        ILocationDirectory locations,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (details, errors) = AnimalRequestValidator.Validate(request, DateOnly.FromDateTime(now.UtcDateTime));
        var microchip = string.IsNullOrWhiteSpace(request.Microchip)
            ? null
            : AnimalRequestValidator.Identifier(request.Microchip, "microchip", errors);

        // Checked even when other fields are invalid, so every error comes back at once.
        var speciesCode = request.SpeciesCode?.Trim();
        if (!string.IsNullOrEmpty(speciesCode)
            && !SpeciesCatalog.IsVisible(await SpeciesCatalog.LoadAsync(db, tenant, cancellationToken), speciesCode))
        {
            errors["speciesCode"] = ["Unknown species."];
        }

        if (microchip is not null && !errors.ContainsKey("microchip") && await MicrochipTakenAsync(db, microchip, cancellationToken))
        {
            errors["microchip"] = [MicrochipTakenMessage];
        }

        if (details is null || errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var number = await NextNumberAsync(db, tenant.RequireTenantId(), cancellationToken);
        var animal = new Animal(number, details, now);
        db.Set<Animal>().Add(animal);
        if (microchip is not null)
        {
            db.Set<AnimalIdentifier>().Add(new AnimalIdentifier(animal.Id, AnimalIdentifierTypes.Microchip, microchip, now));
        }

        timeline.Append(new TimelineEntry(animal.Id, AnimalTimelineTypes.Registered, now, SourceModule, animal.Id));

        if (!await TrySaveAsync(db, cancellationToken))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["microchip"] = [MicrochipTakenMessage] });
        }

        return TypedResults.Created($"/api/animals/{animal.Id:D}", await ResponseAsync(animal, db, locations, cancellationToken));
    }

    /// <summary>Replaces the descriptive fields. 409 when <c>version</c> is not the current one.</summary>
    internal static async Task<Results<Ok<AnimalResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Update(
        Guid animalId,
        UpdateAnimalRequest request,
        ShelterDbContext db,
        ITenantContext tenant,
        ILocationDirectory locations,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (details, errors) = AnimalRequestValidator.Validate(request, DateOnly.FromDateTime(now.UtcDateTime));

        var animal = await db.Set<Animal>().SingleOrDefaultAsync(a => a.Id == animalId, cancellationToken);
        if (animal is null)
        {
            return TypedResults.NotFound();
        }

        // A species hidden after the animal got it stays valid; a new choice must be a visible species.
        var speciesCode = request.SpeciesCode?.Trim();
        if (!string.IsNullOrEmpty(speciesCode)
            && speciesCode != animal.SpeciesCode
            && !SpeciesCatalog.IsVisible(await SpeciesCatalog.LoadAsync(db, tenant, cancellationToken), speciesCode))
        {
            errors["speciesCode"] = ["Unknown species."];
        }

        if (details is null || errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        db.Entry(animal).Property(a => a.Version).OriginalValue = request.Version;
        animal.Update(details, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This animal was changed by someone else. Reload it and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = VersionConflict });
        }

        return TypedResults.Ok(await ResponseAsync(animal, db, locations, cancellationToken));
    }

    /// <summary>Adds a microchip, licence or external number. An active microchip is unique in the organization.</summary>
    internal static async Task<Results<Created<AnimalIdentifierItem>, NotFound, ValidationProblem>> AddIdentifier(
        Guid animalId, AnimalIdentifierRequest request, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (!await db.Set<Animal>().AnyAsync(a => a.Id == animalId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        var type = request.Type?.Trim();
        if (type is null || !AnimalIdentifierTypes.All.Contains(type))
        {
            errors["type"] = ["microchip, licence or external."];
        }

        var value = AnimalRequestValidator.Identifier(request.Value, "value", errors);
        if (errors.Count == 0 && type == AnimalIdentifierTypes.Microchip && await MicrochipTakenAsync(db, value!, cancellationToken))
        {
            errors["value"] = [MicrochipTakenMessage];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var identifier = new AnimalIdentifier(animalId, type!, value!, timeProvider.GetUtcNow());
        db.Set<AnimalIdentifier>().Add(identifier);
        if (!await TrySaveAsync(db, cancellationToken))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [MicrochipTakenMessage] });
        }

        return TypedResults.Created($"/api/animals/{animalId:D}/identifiers/{identifier.Id:D}", AnimalIdentifierItem.From(identifier));
    }

    /// <summary>Deactivates an identifier (wrong, replaced or removed). It stays on the record, inactive.</summary>
    internal static async Task<Results<NoContent, NotFound>> DeactivateIdentifier(
        Guid animalId, Guid identifierId, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var identifier = await db.Set<AnimalIdentifier>()
            .SingleOrDefaultAsync(i => i.Id == identifierId && i.AnimalId == animalId, cancellationToken);
        if (identifier is null)
        {
            return TypedResults.NotFound();
        }

        identifier.Deactivate(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// The organization's next animal number. The counter row stays locked until the transaction ends, so concurrent
    /// creations take numbers one after the other, and a rolled-back creation gives its number back.
    /// </summary>
    private static async Task<int> NextNumberAsync(ShelterDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        // Not composed (no SingleAsync): EF would wrap the INSERT in a sub-select.
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO animals.animal_number_counter (tenant_id, last_number) VALUES ({tenantId}, 1)
            ON CONFLICT (tenant_id) DO UPDATE SET last_number = animal_number_counter.last_number + 1
            RETURNING last_number AS "Value"
            """).ToListAsync(cancellationToken);
        return numbers.Single();
    }

    private static Task<bool> MicrochipTakenAsync(ShelterDbContext db, string value, CancellationToken cancellationToken)
    {
        var normalized = SearchNormalizer.Identifier(value);
        return db.Set<AnimalIdentifier>().AnyAsync(
            i => i.Type == AnimalIdentifierTypes.Microchip && i.IsActive && i.NormalizedValue == normalized, cancellationToken);
    }

    /// <summary>Saves; <see langword="false"/> when a concurrent request took the same microchip first.</summary>
    private static async Task<bool> TrySaveAsync(ShelterDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ActiveMicrochipIndex,
        })
        {
            return false;
        }
    }

    private static IQueryable<Animal> Search(IQueryable<Animal> animals, ShelterDbContext db, string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return animals;
        }

        var text = SearchNormalizer.Text(q);
        var textPattern = text.Length > 0 ? SearchNormalizer.ContainsPattern(text) : string.Empty;
        int? number = int.TryParse(q.Trim().TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        var identifier = SearchNormalizer.Identifier(q);
        var identifiers = db.Set<AnimalIdentifier>().Where(i => i.IsActive && i.NormalizedValue == identifier);

        return animals.Where(a =>
            (textPattern != string.Empty && EF.Functions.Like(a.SearchText, textPattern))
            || a.Number == number
            || (identifier != string.Empty && identifiers.Any(i => i.AnimalId == a.Id)));
    }

    private static async Task<AnimalResponse> ResponseAsync(
        Animal animal, ShelterDbContext db, ILocationDirectory locations, CancellationToken cancellationToken)
    {
        var identifiers = await db.Set<AnimalIdentifier>()
            .AsNoTracking()
            .Where(i => i.AnimalId == animal.Id)
            .OrderByDescending(i => i.IsActive)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        var names = await LocationNamesAsync(locations, [animal.CurrentLocationId], cancellationToken);
        return AnimalResponse.From(animal, NameOf(names, animal.CurrentLocationId), [.. identifiers.Select(AnimalIdentifierItem.From)]);
    }

    private static async Task<IReadOnlyDictionary<Guid, LocationSummary>> LocationNamesAsync(
        ILocationDirectory locations, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        List<Guid> distinct = [.. ids.OfType<Guid>().Distinct()];
        return distinct.Count == 0 ? new Dictionary<Guid, LocationSummary>() : await locations.GetAsync(distinct, cancellationToken);
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, LocationSummary> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var location) ? location.Name : null;

    // Ignoring case, so both "personId" and "foundByPersonId" match "PersonId".
    private static List<Guid> IdsEndingWith(IEnumerable<Dictionary<string, string>> parameters, string suffix) =>
        [.. parameters
            .SelectMany(p => p)
            .Where(p => p.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(p => Guid.TryParse(p.Value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()];

    private static Dictionary<string, string> ResolveNames(
        Dictionary<string, string> parameters,
        IReadOnlyDictionary<Guid, LocationSummary> locationNames,
        Dictionary<Guid, string> personNames)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (!Guid.TryParse(value, out var id))
            {
                continue;
            }

            if (key.EndsWith("LocationId", StringComparison.OrdinalIgnoreCase) && locationNames.TryGetValue(id, out var location))
            {
                names[key] = location.Name;
            }
            else if (key.EndsWith("PersonId", StringComparison.OrdinalIgnoreCase) && personNames.TryGetValue(id, out var person))
            {
                names[key] = person;
            }
        }

        return names;
    }
}
