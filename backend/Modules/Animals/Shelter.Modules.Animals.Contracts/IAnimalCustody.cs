namespace Shelter.Modules.Animals.Contracts;

/// <summary>
/// The animal's custody summary, for the Movements module only (ADR 0021): the ledger is the source of truth and this
/// summary is its projection, written in the same transaction (architecture §7.1). Scoped: it shares the caller's
/// <c>ShelterDbContext</c> and unit of work. Methods stage changes; the caller saves once.
/// </summary>
public interface IAnimalCustody
{
    /// <summary>
    /// Locks the animal row (<c>SELECT … FOR UPDATE</c>) until the transaction ends, so concurrent movements of the same
    /// animal run one after the other, and returns its current summary. <see langword="null"/> when the animal is not
    /// found in the current organization.
    /// </summary>
    Task<AnimalCustodyState?> LockAsync(Guid animalId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the summary of an animal locked by <see cref="LockAsync"/> with <paramref name="updated"/>, provided it is
    /// still <paramref name="expected"/>; otherwise throws <see cref="AnimalCustodyConflictException"/>. Staged only.
    /// </summary>
    void Apply(Guid animalId, AnimalCustodyState expected, AnimalCustodyState updated);
}

/// <summary>An animal's custody summary.</summary>
/// <param name="Status">One of <see cref="CustodyStatuses"/>.</param>
/// <param name="LocationId">Where the animal is while in care.</param>
/// <param name="InCareSince">Start of the current stay.</param>
/// <param name="CurrentIntakeId">The movement that started the current stay.</param>
/// <param name="LastOutcomeCode">Outcome type code of the last outcome, kept after it.</param>
public sealed record AnimalCustodyState(
    string Status,
    Guid? LocationId,
    DateTimeOffset? InCareSince,
    Guid? CurrentIntakeId,
    string? LastOutcomeCode)
{
    /// <summary>A newly registered animal: never in care.</summary>
    public static AnimalCustodyState NotInCare { get; } = new(CustodyStatuses.NotInCare, null, null, null, null);
}

/// <summary>Custody status codes. Stable: stored, filtered on and translated by clients.</summary>
public static class CustodyStatuses
{
    /// <summary>Registered, never taken in (or every intake voided).</summary>
    public const string NotInCare = "not_in_care";

    /// <summary>In the organization's care.</summary>
    public const string InCare = "in_care";

    /// <summary>Left the organization's care.</summary>
    public const string Outcome = "outcome";

    /// <summary>Every status.</summary>
    public static IReadOnlyList<string> All { get; } = [NotInCare, InCare, Outcome];
}

/// <summary>The summary changed between <see cref="IAnimalCustody.LockAsync"/> and <see cref="IAnimalCustody.Apply"/>.</summary>
public sealed class AnimalCustodyConflictException : Exception
{
    /// <inheritdoc />
    public AnimalCustodyConflictException()
        : base("The animal's custody summary is not the expected one.")
    {
    }

    /// <inheritdoc />
    public AnimalCustodyConflictException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public AnimalCustodyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
