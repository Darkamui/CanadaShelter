using Microsoft.Extensions.DependencyInjection;

namespace Shelter.BuildingBlocks.DevSeed;

/// <summary>
/// One module's part of the development seed (ADR 0023). Each module seeds only its own tables, through its own domain
/// code, and passes the IDs other modules need through <see cref="DevSeedContext"/>. Contributors run in
/// <see cref="Order"/>, in one scope and one transaction, as the organization being seeded, with administrator
/// permissions and the caller as the signed-in user. Scoped.
/// </summary>
public interface IDevSeedContributor
{
    /// <summary>
    /// Run order: lower first. Conventions: 5 Platform, 10 Operations, 20 People, 30 Animals, 40 Movements; a later
    /// module picks a free number after the modules whose IDs it reads.
    /// </summary>
    int Order { get; }

    /// <summary>Stages and saves this module's sample data. Saves in batches; never commits (the runner does).</summary>
    Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A record one contributor created and another may use: an ID, a time, and a short code (a location kind, a person
/// role, a species). IDs and codes only, like job arguments.
/// </summary>
/// <param name="Id">The record's ID.</param>
/// <param name="At">When it happened in the seeded history (registration, creation).</param>
/// <param name="Tag">A short code that lets the reader choose, such as <c>kennel</c>, <c>adopter</c> or <c>cat</c>.</param>
public sealed record DevSeedRecord(Guid Id, DateTimeOffset At, string Tag);

/// <summary>
/// The state of one seed run, shared by its contributors: the size factor, a seeded random source (the same seed gives
/// the same data), the time window of the generated history, the records contributors publish for each other, and
/// what each one created (the route's response).
/// </summary>
public sealed class DevSeedContext
{
    /// <summary>How far back the generated history goes.</summary>
    public static readonly TimeSpan History = TimeSpan.FromDays(540);

    private readonly Dictionary<string, List<DevSeedRecord>> _records = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _counts = new(StringComparer.Ordinal);

    public DevSeedContext(int factor, int seed, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        Factor = factor;
        Seed = seed;
        Random = new Random(seed);
        Now = now;
    }

    /// <summary>Size multiplier: each contributor creates its base count times this (1 small, 10 demo, 100 load).</summary>
    public int Factor { get; }

    /// <summary>The random seed of this run.</summary>
    public int Seed { get; }

    /// <summary>The only random source contributors use, so a run is reproducible.</summary>
    public Random Random { get; }

    /// <summary>The run's clock. Generated history ends a little before it.</summary>
    public DateTimeOffset Now { get; }

    /// <summary>The start of the generated history.</summary>
    public DateTimeOffset Start => Now - History;

    /// <summary>What each contributor created, by item name (<c>animals</c>, <c>movements.intake</c>, …).</summary>
    public IReadOnlyDictionary<string, int> Counts => _counts;

    /// <summary><paramref name="baseCount"/> times <see cref="Factor"/>.</summary>
    public int Scaled(int baseCount) => baseCount * Factor;

    /// <summary>Publishes a record under <paramref name="key"/> (see <see cref="DevSeedKeys"/>).</summary>
    public void Publish(string key, DevSeedRecord record)
    {
        if (!_records.TryGetValue(key, out var list))
        {
            _records[key] = list = [];
        }

        list.Add(record);
    }

    /// <summary>The records published under <paramref name="key"/>, in publication order; empty when none.</summary>
    public IReadOnlyList<DevSeedRecord> Records(string key) =>
        _records.TryGetValue(key, out var list) ? list : [];

    /// <summary>Adds <paramref name="count"/> to the created count of <paramref name="item"/>.</summary>
    public void Count(string item, int count = 1) =>
        _counts[item] = _counts.GetValueOrDefault(item) + count;

    /// <summary>A uniformly random element.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[Random.Next(items.Count)];
    }

    /// <summary>An element drawn by weight; weights need not sum to anything in particular.</summary>
    public T Weighted<T>(IReadOnlyList<(T Item, int Weight)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var roll = Random.Next(items.Sum(i => i.Weight));
        foreach (var (item, weight) in items)
        {
            roll -= weight;
            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1].Item;
    }

    /// <summary>Whether an event with probability <paramref name="probability"/> (0–1) happens.</summary>
    public bool Chance(double probability) => Random.NextDouble() < probability;

    /// <summary>A time between <paramref name="from"/> and <paramref name="to"/>, to the second.</summary>
    public DateTimeOffset Between(DateTimeOffset from, DateTimeOffset to)
    {
        var seconds = Math.Max(0, (long)(to - from).TotalSeconds);
        return from.AddSeconds(Random.NextInt64(seconds + 1));
    }
}

/// <summary>Keys of the records contributors publish for each other.</summary>
public static class DevSeedKeys
{
    /// <summary>Operations: active locations that hold animals. Tag: the location kind.</summary>
    public const string HousingLocations = "operations.housing";

    /// <summary>People: active people, one record per role they have. Tag: the role code.</summary>
    public const string People = "people.byRole";

    /// <summary>Animals: registered animals, in registration order. At: registration. Tag: the species code.</summary>
    public const string Animals = "animals.registered";
}

/// <summary>Registration of seed contributors.</summary>
public static class DevSeedServiceCollectionExtensions
{
    /// <summary>Adds a module's seed contributor; call from <c>IModule.AddServices</c>.</summary>
    public static IServiceCollection AddDevSeedContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IDevSeedContributor
    {
        services.AddScoped<IDevSeedContributor, TContributor>();
        return services;
    }
}
