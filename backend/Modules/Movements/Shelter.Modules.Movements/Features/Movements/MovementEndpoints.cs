using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Movements.Authorization;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Features.Movements;

/// <summary>
/// <c>/api/movements</c>: the movement ledger (M3-5). Recording a movement checks the custody rules under the animal's
/// row lock, then stages the movement, the new custody summary (Animals) and a timeline event (Animals) in one unit of
/// work, so all three commit together or not at all (ADR 0021). The rules live in <see cref="MovementRecorder"/>.
/// </summary>
internal static class MovementEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", List)
            .WithName("ListMovements")
            .RequirePermission(MovementPermissions.Read);

        endpoints.MapPost("/intakes", RecordIntake)
            .WithName("RecordIntake")
            .RequirePermission(MovementPermissions.Write);

        endpoints.MapPost("/relocations", RecordRelocation)
            .WithName("RecordRelocation")
            .RequirePermission(MovementPermissions.Write);

        endpoints.MapPost("/outcomes", RecordOutcome)
            .WithName("RecordOutcome")
            .RequirePermission(MovementPermissions.Write);

        endpoints.MapPost("/{movementId:guid}/void", Void)
            .WithName("VoidMovement")
            .RequirePermission(MovementPermissions.Amend);
    }

    /// <summary>
    /// An animal's movements, voids included, newest first. Empty for an animal the organization does not have.
    /// </summary>
    internal static async Task<Ok<IReadOnlyList<MovementItem>>> List(
        [FromQuery(Name = "animalId")] Guid animalId, ShelterDbContext db, MovementRecorder recorder, CancellationToken cancellationToken)
    {
        var movements = await db.Set<Movement>()
            .AsNoTracking()
            .Where(m => m.AnimalId == animalId)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(await recorder.ToItemsAsync(movements, cancellationToken));
    }

    internal static Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> RecordIntake(
        RecordIntakeRequest request, MovementRecorder recorder, CancellationToken cancellationToken) =>
        recorder.IntakeAsync(request, cancellationToken);

    internal static Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> RecordRelocation(
        RecordRelocationRequest request, MovementRecorder recorder, CancellationToken cancellationToken) =>
        recorder.RelocationAsync(request, cancellationToken);

    internal static Task<Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult>> RecordOutcome(
        RecordOutcomeRequest request, MovementRecorder recorder, CancellationToken cancellationToken) =>
        recorder.OutcomeAsync(request, cancellationToken);

    internal static Task<Results<Created<MovementItem>, NotFound, ValidationProblem, ProblemHttpResult>> Void(
        Guid movementId, VoidMovementRequest request, MovementRecorder recorder, CancellationToken cancellationToken) =>
        recorder.VoidAsync(movementId, request, cancellationToken);
}
