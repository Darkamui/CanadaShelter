using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Features.Movements;

/// <summary>
/// One animal's ledger, and the custody summary it projects (architecture §7.1). A movement is <em>in effect</em> unless
/// it is a void or has been voided. In-effect movements apply in <c>(occurred_at, id)</c> order; IDs are version 7, so
/// the later-recorded of two movements at the same time applies last.
/// </summary>
internal sealed class Ledger
{
    private Ledger(IReadOnlyList<Movement> inEffect)
    {
        InEffect = inEffect;
    }

    /// <summary>In-effect movements, in the order they apply.</summary>
    public IReadOnlyList<Movement> InEffect { get; }

    /// <summary>The latest movement still in effect: the only one a void may cancel, and the earliest time for a new one.</summary>
    public Movement? Latest => InEffect.Count == 0 ? null : InEffect[^1];

    public static Ledger Of(IEnumerable<Movement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        var all = movements.ToList();
        var voided = all.Where(m => m.VoidsMovementId is not null).Select(m => m.VoidsMovementId!.Value).ToHashSet();
        return new Ledger([.. all
            .Where(m => m.Type != MovementTypes.Void && !voided.Contains(m.Id))
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)]);
    }

    /// <summary>The summary after every in-effect movement except <paramref name="without"/>.</summary>
    public AnimalCustodyState Project(Guid? without = null) =>
        InEffect.Where(m => m.Id != without).Aggregate(AnimalCustodyState.NotInCare, Next);

    /// <summary>The summary after <paramref name="movement"/> applies to <paramref name="state"/>.</summary>
    public static AnimalCustodyState Next(AnimalCustodyState state, Movement movement)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(movement);
        return movement.Type switch
        {
            MovementTypes.Intake => new AnimalCustodyState(
                CustodyStatuses.InCare, movement.ToLocationId, movement.OccurredAt, movement.Id, state.LastOutcomeCode),
            MovementTypes.Relocation => state with { LocationId = movement.ToLocationId },
            MovementTypes.Outcome => new AnimalCustodyState(CustodyStatuses.Outcome, null, null, null, movement.ReasonCode),
            _ => throw new ArgumentException("Only intakes, relocations and outcomes change custody.", nameof(movement)),
        };
    }
}
