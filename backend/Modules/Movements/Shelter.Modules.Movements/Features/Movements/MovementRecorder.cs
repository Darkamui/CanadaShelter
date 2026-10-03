using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Movements.Domain;
using Shelter.Modules.Movements.Features.ReferenceData;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.People.Contracts;

namespace Shelter.Modules.Movements.Features.Movements;

/// <summary>
/// Records movements under the custody rules (M3-5). Every write locks the animal first
/// (<see cref="IAnimalCustody.LockAsync"/>), so movements of one animal are serialized; it then checks the rules against
/// the ledger, locks the target location against archiving, and stages the movement, the new summary and a timeline
/// event. The request's unit of work saves them in one transaction. Scoped.
/// </summary>
internal sealed class MovementRecorder(
    ShelterDbContext db,
    ITenantContext tenant,
    IUserContext user,
    IPermissionContext permissions,
    IAnimalCustody custody,
    IAnimalTimeline timeline,
    ILocationDirectory locations,
    IPersonDirectory people,
    TimeProvider timeProvider)
{
    /// <summary>The <c>code</c> of the 409 returned when the animal's custody does not allow the movement.</summary>
    public const string CustodyConflict = "movement.custodyConflict";

    /// <summary>The <c>code</c> of the 409 returned when voiding a movement that is not the latest in effect.</summary>
    public const string NotLatest = "movement.notLatest";

    /// <summary>How far ahead of the server clock <c>occurredAt</c> may be (clock skew).</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private const string SourceModule = "movements";

    public async Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> IntakeAsync(
        RecordIntakeRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        Require(request.AnimalId, "animalId", errors);
        Require(request.ToLocationId, "toLocationId", errors);
        var reasonCode = request.ReasonCode?.Trim();
        if (string.IsNullOrEmpty(reasonCode))
        {
            errors["reasonCode"] = ["Required."];
        }

        var notes = Notes(request.Notes, errors);
        if (request.PersonId is not null && !permissions.Has(PeoplePermissionNames.Read))
        {
            return PersonForbidden();
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var animalId = request.AnimalId!.Value;
        var current = await custody.LockAsync(animalId, cancellationToken);
        if (current is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["animalId"] = ["Unknown animal."] });
        }

        if (current.Status == CustodyStatuses.InCare)
        {
            return Conflict("The animal is already in care.");
        }

        if (current.Status == CustodyStatuses.Outcome && current.LastOutcomeCode is not null)
        {
            // Hidden outcome types are included. Codes are never deleted, so an unknown one is a data error: refuse
            // rather than risk taking in an animal that died.
            var outcomes = await ReasonCatalog.OutcomeTypesAsync(db, tenant, cancellationToken);
            if (!outcomes.TryGetValue(current.LastOutcomeCode, out var last) || SacCategories.Outcome.IsTerminal(last.SacCategory))
            {
                return Conflict("The animal died; it cannot be taken in again.");
            }
        }

        var ledger = await LedgerAsync(animalId, cancellationToken);
        var now = Now();
        var occurredAt = OccurredAt(request.OccurredAt, ledger, now, errors);

        var reasons = await ReasonCatalog.IntakeReasonsAsync(db, tenant, cancellationToken);
        if (ReasonCatalog.FindVisible(reasons, reasonCode) is null)
        {
            errors["reasonCode"] = ["Unknown intake reason."];
        }

        var toLocationId = request.ToLocationId!.Value;
        if (!await locations.LockForPlacementAsync(toLocationId, cancellationToken))
        {
            errors["toLocationId"] = ["Choose an active location that holds animals."];
        }

        await CheckPersonAsync(request.PersonId, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var movement = Movement.Intake(
            animalId, reasonCode!, toLocationId, request.PersonId, notes, occurredAt, now, RecordedBy());
        return await StageAsync(
            movement, current, "intake_recorded",
            Parameters(("reasonCode", reasonCode), ("toLocationId", Id(toLocationId)), ("personId", Id(request.PersonId))),
            cancellationToken);
    }

    public async Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> RelocationAsync(
        RecordRelocationRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        Require(request.AnimalId, "animalId", errors);
        Require(request.ToLocationId, "toLocationId", errors);
        var notes = Notes(request.Notes, errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var animalId = request.AnimalId!.Value;
        var current = await custody.LockAsync(animalId, cancellationToken);
        if (current is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["animalId"] = ["Unknown animal."] });
        }

        if (current.Status != CustodyStatuses.InCare || current.LocationId is null)
        {
            return Conflict("The animal is not in care.");
        }

        var ledger = await LedgerAsync(animalId, cancellationToken);
        var now = Now();
        var occurredAt = OccurredAt(request.OccurredAt, ledger, now, errors);

        var fromLocationId = current.LocationId.Value;
        var toLocationId = request.ToLocationId!.Value;
        if (toLocationId == fromLocationId)
        {
            errors["toLocationId"] = ["The animal is already at this location."];
        }
        else if (!await locations.LockForPlacementAsync(toLocationId, cancellationToken))
        {
            errors["toLocationId"] = ["Choose an active location that holds animals."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var movement = Movement.Relocation(animalId, fromLocationId, toLocationId, notes, occurredAt, now, RecordedBy());
        return await StageAsync(
            movement, current, "relocation_recorded",
            Parameters(("fromLocationId", Id(fromLocationId)), ("toLocationId", Id(toLocationId))),
            cancellationToken);
    }

    public async Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> OutcomeAsync(
        RecordOutcomeRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        Require(request.AnimalId, "animalId", errors);
        var outcomeCode = request.OutcomeCode?.Trim();
        if (string.IsNullOrEmpty(outcomeCode))
        {
            errors["outcomeCode"] = ["Required."];
        }

        var notes = Notes(request.Notes, errors);
        if (request.PersonId is not null && !permissions.Has(PeoplePermissionNames.Read))
        {
            return PersonForbidden();
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var animalId = request.AnimalId!.Value;
        var current = await custody.LockAsync(animalId, cancellationToken);
        if (current is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["animalId"] = ["Unknown animal."] });
        }

        if (current.Status != CustodyStatuses.InCare || current.LocationId is null)
        {
            return Conflict("The animal is not in care.");
        }

        var ledger = await LedgerAsync(animalId, cancellationToken);
        var now = Now();
        var occurredAt = OccurredAt(request.OccurredAt, ledger, now, errors);

        var outcome = ReasonCatalog.FindVisible(await ReasonCatalog.OutcomeTypesAsync(db, tenant, cancellationToken), outcomeCode);
        if (outcome is null)
        {
            errors["outcomeCode"] = ["Unknown outcome type."];
        }
        else if (request.PersonId is null && SacCategories.Outcome.RequiresPerson(outcome.Value.SacCategory))
        {
            errors["personId"] = ["Required for this outcome."];
        }

        await CheckPersonAsync(request.PersonId, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var fromLocationId = current.LocationId.Value;
        var movement = Movement.Outcome(
            animalId, outcomeCode!, fromLocationId, request.PersonId, notes, occurredAt, now, RecordedBy());
        return await StageAsync(
            movement, current, "outcome_recorded",
            Parameters(("outcomeCode", outcomeCode), ("fromLocationId", Id(fromLocationId)), ("personId", Id(request.PersonId))),
            cancellationToken);
    }

    /// <summary>
    /// Voids the animal's latest movement in effect and restores the summary the ledger projects without it. Earlier
    /// movements cannot be voided (they would rewrite later ones).
    /// </summary>
    public async Task<Results<Created<MovementItem>, NotFound, ValidationProblem, ProblemHttpResult>> VoidAsync(
        Guid movementId, VoidMovementRequest request, CancellationToken cancellationToken)
    {
        var target = await db.Set<Movement>().AsNoTracking().SingleOrDefaultAsync(m => m.Id == movementId, cancellationToken);
        if (target is null)
        {
            return TypedResults.NotFound();
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Required."] });
        }

        if (reason.Length > Movement.NotesMaxLength)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["reason"] = [$"At most {Movement.NotesMaxLength} characters."] });
        }

        var current = await custody.LockAsync(target.AnimalId, cancellationToken);
        if (current is null)
        {
            return TypedResults.NotFound();
        }

        // Read the ledger after the lock: a concurrent void or movement has committed by now.
        var ledger = await LedgerAsync(target.AnimalId, cancellationToken);
        if (ledger.Latest?.Id != target.Id)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Only the animal's latest movement still in effect can be voided.",
                extensions: new Dictionary<string, object?> { ["code"] = NotLatest });
        }

        var restored = ledger.Project(without: target.Id);
        if (restored.Status == CustodyStatuses.InCare
            && (restored.LocationId is null || !await locations.LockForPlacementAsync(restored.LocationId.Value, cancellationToken)))
        {
            return Conflict("The animal's previous location is archived or no longer holds animals.");
        }

        var voiding = Movement.Void(target, reason, Now(), RecordedBy());
        db.Set<Movement>().Add(voiding);
        try
        {
            custody.Apply(target.AnimalId, current, restored);
        }
        catch (AnimalCustodyConflictException)
        {
            return Conflict("The animal's custody changed. Reload it and try again.");
        }

        timeline.Append(new TimelineEntry(
            target.AnimalId, "movement_voided", voiding.OccurredAt, SourceModule, voiding.Id,
            Parameters(("movementType", target.Type), ("voidedMovementId", Id(target.Id)))));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/movements?animalId={target.AnimalId:D}", (await ToItemsAsync([voiding], cancellationToken))[0]);
    }

    /// <summary>
    /// Movements as API items, with location and person names resolved. Person ID, name and notes are left out without
    /// <c>person.read</c>.
    /// </summary>
    public async Task<IReadOnlyList<MovementItem>> ToItemsAsync(IReadOnlyList<Movement> movements, CancellationToken cancellationToken)
    {
        if (movements.Count == 0)
        {
            return [];
        }

        var ids = movements.Select(m => m.Id).ToList();
        var voidedBy = await db.Set<Movement>()
            .AsNoTracking()
            .Where(m => m.VoidsMovementId != null && ids.Contains(m.VoidsMovementId.Value))
            .Select(m => new { m.Id, Voided = m.VoidsMovementId!.Value })
            .ToDictionaryAsync(m => m.Voided, m => m.Id, cancellationToken);

        var locationIds = movements
            .SelectMany(m => new[] { m.FromLocationId, m.ToLocationId })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var locationNames = locationIds.Count == 0
            ? new Dictionary<Guid, LocationSummary>()
            : await locations.GetAsync(locationIds, cancellationToken);

        var canReadPeople = permissions.Has(PeoplePermissionNames.Read);
        var personIds = canReadPeople ? movements.Select(m => m.PersonId).OfType<Guid>().Distinct().ToList() : [];
        var personNames = personIds.Count == 0
            ? new Dictionary<Guid, PersonSummary>()
            : await people.GetSummariesAsync(personIds, cancellationToken);

        return [.. movements.Select(m => new MovementItem(
            m.Id,
            m.Type,
            m.AnimalId,
            m.ReasonCode,
            m.FromLocationId,
            m.FromLocationId is { } from && locationNames.TryGetValue(from, out var f) ? f.Name : null,
            m.ToLocationId,
            m.ToLocationId is { } to && locationNames.TryGetValue(to, out var t) ? t.Name : null,
            canReadPeople ? m.PersonId : null,
            canReadPeople && m.PersonId is { } p && personNames.TryGetValue(p, out var person) ? person.DisplayName : null,
            canReadPeople ? m.Notes : null,
            m.OccurredAt,
            m.RecordedAt,
            m.VoidsMovementId,
            voidedBy.TryGetValue(m.Id, out var by) ? by : null))];
    }

    private async Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> StageAsync(
        Movement movement, AnimalCustodyState current, string timelineType, Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        db.Set<Movement>().Add(movement);
        try
        {
            custody.Apply(movement.AnimalId, current, Ledger.Next(current, movement));
        }
        catch (AnimalCustodyConflictException)
        {
            return Conflict("The animal's custody changed. Reload it and try again.");
        }

        timeline.Append(new TimelineEntry(movement.AnimalId, timelineType, movement.OccurredAt, SourceModule, movement.Id, parameters));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/movements?animalId={movement.AnimalId:D}", (await ToItemsAsync([movement], cancellationToken))[0]);
    }

    private async Task<Ledger> LedgerAsync(Guid animalId, CancellationToken cancellationToken) =>
        Ledger.Of(await db.Set<Movement>().AsNoTracking().Where(m => m.AnimalId == animalId).ToListAsync(cancellationToken));

    private async Task CheckPersonAsync(Guid? personId, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (personId is { } id && !await people.ExistsActiveAsync(id, cancellationToken))
        {
            errors["personId"] = ["Choose an active person."];
        }
    }

    /// <summary>
    /// <paramref name="requested"/> or now, at PostgreSQL's microsecond precision so the stored summary compares equal.
    /// Not before the latest movement in effect (the ledger stays in order), and not in the future.
    /// </summary>
    private static DateTimeOffset OccurredAt(
        DateTimeOffset? requested, Ledger ledger, DateTimeOffset now, Dictionary<string, string[]> errors)
    {
        var value = Truncate((requested ?? now).ToUniversalTime());
        if (value > now + FutureTolerance)
        {
            errors["occurredAt"] = ["Cannot be in the future."];
        }
        else if (ledger.Latest is { } latest && value < latest.OccurredAt)
        {
            errors["occurredAt"] = ["Cannot be before the animal's latest movement."];
        }

        return value;
    }

    private static DateTimeOffset Truncate(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % 10), value.Offset);

    private DateTimeOffset Now() => Truncate(timeProvider.GetUtcNow());

    private Guid RecordedBy() =>
        user.UserId ?? throw new InvalidOperationException("Movements are recorded by a signed-in account.");

    private static void Require(Guid? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null || value == Guid.Empty)
        {
            errors[field] = ["Required."];
        }
    }

    private static string? Notes(string? notes, Dictionary<string, string[]> errors)
    {
        var value = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (value is { Length: > Movement.NotesMaxLength })
        {
            errors["notes"] = [$"At most {Movement.NotesMaxLength} characters."];
        }

        return value;
    }

    private static string? Id(Guid? id) => id?.ToString("D");

    private static Dictionary<string, string> Parameters(params (string Key, string? Value)[] values) =>
        values.Where(v => v.Value is not null).ToDictionary(v => v.Key, v => v.Value!);

    private static ProblemHttpResult Conflict(string title) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = CustodyConflict });

    // A person ID links the animal to a person: recording one needs the same right as reading it. Not Forbid(), which
    // the cookie scheme would turn into a redirect.
    private static ProblemHttpResult PersonForbidden() =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Linking a person requires person.read.");
}
