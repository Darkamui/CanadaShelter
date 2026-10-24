using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Movements.Domain;

/// <summary>
/// One entry of the movement ledger (architecture §7.1): the source of truth for custody. Tenant-owned and append-only:
/// the runtime role has SELECT and INSERT only, so a mistake is corrected by a new <see cref="MovementTypes.Void"/>
/// row, never by editing. <see cref="Notes"/> is personal data about <see cref="PersonId"/> (the audit subject); the
/// rest are IDs, codes and times.
/// </summary>
internal sealed class Movement : ITenantOwned
{
    public const int NotesMaxLength = 2000;
    public const int CodeMaxLength = 50;

    private Movement()
    {
        Type = null!;
    }

    private Movement(
        string type,
        Guid animalId,
        string? reasonCode,
        Guid? fromLocationId,
        Guid? toLocationId,
        Guid? personId,
        string? notes,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        Guid recordedByUserId,
        Guid? voidsMovementId)
    {
        Id = Guid.CreateVersion7();
        Type = type;
        AnimalId = animalId;
        ReasonCode = reasonCode;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        PersonId = personId;
        Notes = notes;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        RecordedByUserId = recordedByUserId;
        VoidsMovementId = voidsMovementId;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>One of <see cref="MovementTypes"/>.</summary>
    public string Type { get; private set; }

    public Guid AnimalId { get; private set; }

    /// <summary>Intake reason code (intake) or outcome type code (outcome); null otherwise.</summary>
    public string? ReasonCode { get; private set; }

    /// <summary>Where the animal was: set on relocations and outcomes.</summary>
    public Guid? FromLocationId { get; private set; }

    /// <summary>Where the animal went: set on intakes and relocations.</summary>
    public Guid? ToLocationId { get; private set; }

    /// <summary>The person involved (finder, surrenderer, adopter, owner), if any.</summary>
    public Guid? PersonId { get; private set; }

    /// <summary>Free text; on a void, the reason. Personal data.</summary>
    public string? Notes { get; private set; }

    /// <summary>When it happened. Never before the latest movement still in effect.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>The staff account that recorded it.</summary>
    public Guid RecordedByUserId { get; private set; }

    /// <summary>On a void: the movement it cancels.</summary>
    public Guid? VoidsMovementId { get; private set; }

    public static Movement Intake(
        Guid animalId, string reasonCode, Guid toLocationId, Guid? personId, string? notes, DateTimeOffset occurredAt,
        DateTimeOffset recordedAt, Guid recordedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new(MovementTypes.Intake, animalId, reasonCode, null, toLocationId, personId, notes, occurredAt, recordedAt, recordedBy, null);
    }

    public static Movement Relocation(
        Guid animalId, Guid fromLocationId, Guid toLocationId, string? notes, DateTimeOffset occurredAt, DateTimeOffset recordedAt,
        Guid recordedBy) =>
        new(MovementTypes.Relocation, animalId, null, fromLocationId, toLocationId, null, notes, occurredAt, recordedAt, recordedBy, null);

    public static Movement Outcome(
        Guid animalId, string outcomeCode, Guid fromLocationId, Guid? personId, string? notes, DateTimeOffset occurredAt,
        DateTimeOffset recordedAt, Guid recordedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeCode);
        return new(MovementTypes.Outcome, animalId, outcomeCode, fromLocationId, null, personId, notes, occurredAt, recordedAt, recordedBy, null);
    }

    /// <summary>
    /// Cancels <paramref name="voided"/>. The void keeps the voided movement's person, so the reason is shredded with
    /// that person's other notes.
    /// </summary>
    public static Movement Void(Movement voided, string reason, DateTimeOffset recordedAt, Guid recordedBy)
    {
        ArgumentNullException.ThrowIfNull(voided);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (voided.Type == MovementTypes.Void)
        {
            throw new ArgumentException("A void cannot be voided.", nameof(voided));
        }

        return new(MovementTypes.Void, voided.AnimalId, null, null, null, voided.PersonId, reason, recordedAt, recordedAt, recordedBy, voided.Id);
    }
}

/// <summary>Movement type codes. Stable: stored, and translated by clients.</summary>
internal static class MovementTypes
{
    public const string Intake = "intake";
    public const string Relocation = "relocation";
    public const string Outcome = "outcome";
    public const string Void = "void";

    public static IReadOnlyList<string> All { get; } = [Intake, Relocation, Outcome, Void];
}
