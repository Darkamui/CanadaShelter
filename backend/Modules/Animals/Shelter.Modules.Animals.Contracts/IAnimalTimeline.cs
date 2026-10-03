namespace Shelter.Modules.Animals.Contracts;

/// <summary>
/// Appends to an animal's timeline (architecture §7.1): the readable history staff see. Append-only; the runtime role
/// cannot update or delete a row. Scoped (ADR 0021): the event is staged in the caller's unit of work and saved with the
/// caller's own change, so the record and its timeline event commit together or not at all.
/// </summary>
public interface IAnimalTimeline
{
    /// <summary>Stages <paramref name="entry"/>. The caller saves.</summary>
    /// <exception cref="ArgumentException">A parameter key is not an identifier, or a value is neither an ID nor a
    /// short lowercase code.</exception>
    void Append(TimelineEntry entry);
}

/// <summary>
/// One timeline event. <paramref name="Parameters"/> hold codes and IDs only, never names or free text, so the timeline
/// holds no personal data: names are resolved when it is read. A key ending in <c>LocationId</c> or <c>PersonId</c>
/// is resolved to that location's or person's name.
/// </summary>
/// <param name="AnimalId">The animal.</param>
/// <param name="Type">Event type code, such as <c>animal_registered</c>; clients translate it.</param>
/// <param name="OccurredAt">When it happened (may be earlier than when it was recorded).</param>
/// <param name="SourceModule">Module that recorded it, such as <c>animals</c> or <c>movements</c>.</param>
/// <param name="SourceRecordId">ID of the record in that module, if any.</param>
/// <param name="Parameters">Codes and IDs only.</param>
public sealed record TimelineEntry(
    Guid AnimalId,
    string Type,
    DateTimeOffset OccurredAt,
    string SourceModule,
    Guid? SourceRecordId,
    IReadOnlyDictionary<string, string>? Parameters = null);
