namespace Shelter.Modules.Movements.Features.Movements;

/// <summary>
/// Takes an animal into care at <c>toLocationId</c>. <c>occurredAt</c> defaults to now; it may be in the past, but not
/// before the animal's latest movement still in effect, nor more than 5 minutes ahead.
/// </summary>
internal sealed record RecordIntakeRequest(
    Guid? AnimalId, string? ReasonCode, Guid? ToLocationId, Guid? PersonId, string? Notes, DateTimeOffset? OccurredAt);

/// <summary>Moves an animal in care from its current location to <c>toLocationId</c>.</summary>
internal sealed record RecordRelocationRequest(Guid? AnimalId, Guid? ToLocationId, string? Notes, DateTimeOffset? OccurredAt);

/// <summary>Ends an animal's stay. Adoption and return to owner need <c>personId</c>.</summary>
internal sealed record RecordOutcomeRequest(
    Guid? AnimalId, string? OutcomeCode, Guid? PersonId, string? Notes, DateTimeOffset? OccurredAt);

/// <summary>Cancels the animal's latest movement still in effect; <c>reason</c> is required.</summary>
internal sealed record VoidMovementRequest(string? Reason);

/// <summary>
/// One movement as staff see it. <c>personId</c>, <c>personName</c> and <c>notes</c> are personal data: they are null
/// unless the caller has <c>person.read</c>. Location names are resolved when read.
/// </summary>
/// <param name="Id">Movement ID.</param>
/// <param name="Type"><c>intake</c>, <c>relocation</c>, <c>outcome</c> or <c>void</c>.</param>
/// <param name="AnimalId">The animal.</param>
/// <param name="ReasonCode">Intake reason (intake) or outcome type (outcome) code.</param>
/// <param name="FromLocationId">Where the animal was (relocation, outcome).</param>
/// <param name="FromLocationName">Its name.</param>
/// <param name="ToLocationId">Where the animal went (intake, relocation).</param>
/// <param name="ToLocationName">Its name.</param>
/// <param name="PersonId">The person involved.</param>
/// <param name="PersonName">Their display name.</param>
/// <param name="Notes">Notes; on a void, its reason.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="RecordedAt">When it was recorded.</param>
/// <param name="VoidsMovementId">On a void: the movement it cancels.</param>
/// <param name="VoidedByMovementId">The void that cancelled this movement, if any.</param>
internal sealed record MovementItem(
    Guid Id,
    string Type,
    Guid AnimalId,
    string? ReasonCode,
    Guid? FromLocationId,
    string? FromLocationName,
    Guid? ToLocationId,
    string? ToLocationName,
    Guid? PersonId,
    string? PersonName,
    string? Notes,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    Guid? VoidsMovementId,
    Guid? VoidedByMovementId);
