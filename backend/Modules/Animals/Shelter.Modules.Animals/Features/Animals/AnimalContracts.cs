using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Features.Animals;

/// <summary><see cref="IAnimalCustody"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021).</summary>
internal sealed class AnimalCustody(ShelterDbContext db, TimeProvider timeProvider) : IAnimalCustody
{
    public async Task<AnimalCustodyState?> LockAsync(Guid animalId, CancellationToken cancellationToken)
    {
        // Lock first, then read: the values read are the ones committed before the lock was granted. Not SELECT *:
        // EF needs xmin, which a raw row would not carry.
        var locked = await db.Database
            .SqlQuery<Guid>($"""SELECT id AS "Value" FROM animals.animal WHERE id = {animalId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        if (locked.Count == 0)
        {
            return null;
        }

        var tracked = db.Set<Animal>().Local.FirstOrDefault(a => a.Id == animalId);
        if (tracked is not null)
        {
            // Reloading would silently discard unsaved edits to the animal.
            if (db.Entry(tracked).State != EntityState.Unchanged)
            {
                throw new InvalidOperationException("Lock the animal before changing it in the same unit of work.");
            }

            await db.Entry(tracked).ReloadAsync(cancellationToken);
            return tracked.Custody;
        }

        var animal = await db.Set<Animal>().SingleAsync(a => a.Id == animalId, cancellationToken);
        return animal.Custody;
    }

    public void Apply(Guid animalId, AnimalCustodyState expected, AnimalCustodyState updated)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);

        var animal = db.Set<Animal>().Local.FirstOrDefault(a => a.Id == animalId)
            ?? throw new InvalidOperationException("Lock the animal with LockAsync before applying a custody change.");
        if (animal.Custody != expected)
        {
            throw new AnimalCustodyConflictException();
        }

        animal.ApplyCustody(updated, timeProvider.GetUtcNow());
    }
}

/// <summary>
/// <see cref="IAnimalTimeline"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021). Parameter keys must be
/// identifiers and values IDs or short codes, so no name or note can reach the append-only table, which cannot be
/// corrected or shredded.
/// </summary>
internal sealed partial class AnimalTimeline(ShelterDbContext db, TimeProvider timeProvider) : IAnimalTimeline
{
    public void Append(TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        foreach (var (key, value) in entry.Parameters ?? new Dictionary<string, string>())
        {
            if (!KeyPattern().IsMatch(key) || value is null || !(Guid.TryParse(value, out _) || CodePattern().IsMatch(value)))
            {
                throw new ArgumentException("Timeline parameters hold IDs and codes only.", nameof(entry));
            }
        }

        var parameters = JsonSerializer.Serialize(entry.Parameters ?? new Dictionary<string, string>());
        db.Set<TimelineEvent>().Add(new TimelineEvent(
            entry.AnimalId, entry.Type, entry.OccurredAt, timeProvider.GetUtcNow(), entry.SourceModule, entry.SourceRecordId, parameters));
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,49}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z0-9_.-]{1,50}$")]
    private static partial Regex CodePattern();
}

/// <summary><see cref="IAnimalPopulation"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021). Read-only.</summary>
internal sealed class AnimalPopulation(ShelterDbContext db) : IAnimalPopulation
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountAtAsync(
        IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locationIds);
        if (locationIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        List<Guid?> ids = [.. locationIds.Distinct().Select(id => (Guid?)id)];
        return await db.Set<Animal>()
            .AsNoTracking()
            .Where(a => a.CustodyStatus == CustodyStatuses.InCare && ids.Contains(a.CurrentLocationId))
            .GroupBy(a => a.CurrentLocationId!.Value)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.LocationId, g => g.Count, cancellationToken);
    }
}
