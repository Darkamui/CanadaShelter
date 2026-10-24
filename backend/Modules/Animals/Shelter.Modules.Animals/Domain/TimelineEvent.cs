using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Animals.Domain;

/// <summary>
/// One event of an animal's timeline (architecture §7.1). Tenant-owned and append-only: the runtime role has SELECT
/// and INSERT only. Parameters are codes and IDs, never names or free text, so no personal data is stored here; names
/// are resolved when the timeline is read.
/// </summary>
internal sealed class TimelineEvent : ITenantOwned
{
    public const int TypeMaxLength = 50;
    public const int SourceModuleMaxLength = 50;

    private TimelineEvent()
    {
        Type = null!;
        SourceModule = null!;
        Parameters = null!;
    }

    public TimelineEvent(
        Guid animalId,
        string type,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        string sourceModule,
        Guid? sourceRecordId,
        string parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters);

        Id = Guid.CreateVersion7();
        AnimalId = animalId;
        Type = type;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        SourceModule = sourceModule;
        SourceRecordId = sourceRecordId;
        Parameters = parameters;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public Guid AnimalId { get; private set; }

    /// <summary>Event type code, such as <c>animal_registered</c>.</summary>
    public string Type { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Module that recorded the event.</summary>
    public string SourceModule { get; private set; }

    /// <summary>ID of the source record in that module.</summary>
    public Guid? SourceRecordId { get; private set; }

    /// <summary>A JSON object of string codes and IDs (<c>jsonb</c>).</summary>
    public string Parameters { get; private set; }
}

/// <summary>The timeline event types recorded by the Animals module.</summary>
internal static class AnimalTimelineTypes
{
    public const string Registered = "animal_registered";
}

/// <summary>
/// The per-organization animal number counter: one row per organization, incremented in the creating transaction, so
/// numbers have no gaps (a rolled-back creation gives its number back). Tenant-owned; written with raw SQL only.
/// </summary>
internal sealed class AnimalNumberCounter : ITenantOwned
{
    private AnimalNumberCounter()
    {
    }

    public AnimalNumberCounter(int lastNumber)
    {
        LastNumber = lastNumber;
    }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>The last number given out.</summary>
    public int LastNumber { get; private set; }
}
