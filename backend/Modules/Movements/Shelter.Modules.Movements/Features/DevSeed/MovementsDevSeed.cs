using Microsoft.AspNetCore.Http.HttpResults;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Movements.Features.Movements;
using Shelter.Modules.Movements.Features.ReferenceData;

namespace Shelter.Modules.Movements.Features.DevSeed;

/// <summary>
/// Development seed (ADR 0023): a custody history for each seeded animal, recorded through <see cref="MovementRecorder"/>
/// so the ledger, the custody summary and the timeline stay consistent. Intake shortly after registration, a stay of a
/// few days to three months with some relocations, then an outcome, unless the stay reaches today (the animal is still
/// in care). Some adoptions come back, a few animals are never taken in, and a few latest movements are voided.
/// </summary>
internal sealed class MovementsDevSeed(ShelterDbContext db, ITenantContext tenant, MovementRecorder recorder) : IDevSeedContributor
{
    private const int ClearEvery = 50;

    private static readonly (string Code, int Weight)[] IntakeWeights =
    [
        ("stray", 40), ("owner_surrender", 25), ("transfer_in", 10), ("seizure", 5), ("born_in_care", 5), ("other", 3),
    ];

    private static readonly (string Code, int Weight)[] OutcomeWeights =
    [
        ("adoption", 55), ("return_to_owner", 15), ("transfer_out", 12), ("return_to_field", 5), ("died", 3),
        ("euthanized", 2), ("other", 3),
    ];

    private DevSeedContext _context = null!;
    private Dictionary<string, List<Guid>> _housing = null!;
    private Dictionary<string, List<Guid>> _people = null!;
    private List<(string Code, int Weight)> _intakeReasons = null!;
    private List<(string Code, int Weight)> _outcomes = null!;
    private DateTimeOffset _limit;
    private Guid _location;

    public int Order => 40;

    public async Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken)
    {
        _context = context;
        _limit = context.Now.AddMinutes(-1);
        _housing = Group(context.Records(DevSeedKeys.HousingLocations));
        _people = Group(context.Records(DevSeedKeys.People));
        if (_housing.Count == 0)
        {
            return;
        }

        var reasons = await ReasonCatalog.IntakeReasonsAsync(db, tenant, cancellationToken);
        var outcomes = await ReasonCatalog.OutcomeTypesAsync(db, tenant, cancellationToken);
        _intakeReasons = [.. IntakeWeights.Where(r => ReasonCatalog.FindVisible(reasons, r.Code) is not null)];
        _outcomes = [.. OutcomeWeights.Where(o => ReasonCatalog.FindVisible(outcomes, o.Code) is not null)];
        var canReturn = ReasonCatalog.FindVisible(reasons, "returned_adoption") is not null;
        if (_intakeReasons.Count == 0)
        {
            return;
        }

        var done = 0;
        foreach (var animal in context.Records(DevSeedKeys.Animals))
        {
            if (!context.Chance(0.03))
            {
                await HistoryAsync(animal, canReturn, cancellationToken);
            }

            if (++done % ClearEvery == 0)
            {
                // The recorder reloads custody from the database on every call; only the tracked entities pile up.
                db.ChangeTracker.Clear();
            }
        }

        db.ChangeTracker.Clear();
    }

    private async Task HistoryAsync(DevSeedRecord animal, bool canReturn, CancellationToken cancellationToken)
    {
        var species = animal.Tag;
        var at = Clamp(animal.At.AddMinutes(_context.Random.Next(5, 61)));
        var reason = _context.Weighted(_intakeReasons);
        var last = await IntakeAsync(animal.Id, species, reason, at, cancellationToken);
        var returned = false;

        while (true)
        {
            // Mostly short stays, a long tail up to three months.
            var r = _context.Random.NextDouble();
            var end = at.AddDays(2 + (88 * r * r)).AddMinutes(_context.Random.Next(0, 600));
            var moves = _context.Weighted([(0, 60), (1, 30), (2, 10)]);
            for (var i = 1; i <= moves; i++)
            {
                var moveAt = at + ((end - at) * i / (moves + 1));
                if (moveAt >= _limit)
                {
                    break;
                }

                var target = _context.Chance(0.15) && _housing.TryGetValue("external_clinic", out var clinics)
                    ? _context.Pick(clinics)
                    : Housing(species);
                if (target == _location)
                {
                    continue;
                }

                _location = target;
                last = Expect(
                    await recorder.RelocationAsync(
                        new RecordRelocationRequest(animal.Id, target, Note(0.1), moveAt), cancellationToken),
                    "relocation");
                at = moveAt;
            }

            if (end >= _limit)
            {
                _context.Count("animals.inCare");
                break;
            }

            var outcome = Outcome(species);
            last = Expect(
                await recorder.OutcomeAsync(
                    new RecordOutcomeRequest(animal.Id, outcome, PersonFor(outcome), Note(0.1), end), cancellationToken),
                "outcome." + outcome);
            at = end;

            if (outcome != "adoption" || returned || !canReturn || !_context.Chance(0.08))
            {
                break;
            }

            var back = end.AddDays(_context.Random.Next(3, 31));
            if (back >= _limit)
            {
                break;
            }

            returned = true;
            last = await IntakeAsync(animal.Id, species, "returned_adoption", back, cancellationToken);
            at = back;
        }

        if (_context.Chance(0.01))
        {
            var voided = await recorder.VoidAsync(last, new VoidMovementRequest("Saisie en double"), cancellationToken);
            if (voided.Result is not Created<MovementItem>)
            {
                throw new InvalidOperationException("Dev seed could not void a movement: " + Describe(voided.Result));
            }

            _context.Count("movements.voided");
        }
    }

    private async Task<Guid> IntakeAsync(
        Guid animalId, string species, string reason, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var person = reason switch
        {
            "stray" when _context.Chance(0.6) => Person("finder"),
            "owner_surrender" or "returned_adoption" => Person(reason == "returned_adoption" ? "adopter" : "surrenderer"),
            _ => null,
        };
        var location = Housing(species);
        _location = location;
        return Expect(
            await recorder.IntakeAsync(new RecordIntakeRequest(animalId, reason, location, person, Note(0.2), at), cancellationToken),
            "intake." + reason);
    }

    private string Outcome(string species)
    {
        while (true)
        {
            var code = _context.Weighted(_outcomes);
            var needsPerson = code is "adoption" or "return_to_owner";
            if ((code != "return_to_field" || species == "cat") && (!needsPerson || PersonFor(code) is not null))
            {
                return code;
            }

            if (_outcomes.All(o => o.Code is "return_to_field" or "adoption" or "return_to_owner"))
            {
                // Nothing else to pick for this animal: transfer it out if possible.
                return _outcomes[0].Code;
            }
        }
    }

    private Guid? PersonFor(string outcome) => outcome switch
    {
        "adoption" => Person("adopter"),
        "return_to_owner" => Person("owner"),
        _ => null,
    };

    private Guid? Person(string role) =>
        _people.TryGetValue(role, out var ids) ? _context.Pick(ids) : null;

    /// <summary>Where an animal of <paramref name="species"/> is housed: dogs in kennels, cats in cages, others in rooms.</summary>
    private Guid Housing(string species)
    {
        string[] kinds = species switch
        {
            "dog" => _context.Chance(0.1) ? ["isolation"] : ["kennel"],
            "cat" => _context.Chance(0.1) ? ["isolation"] : ["cage"],
            _ => ["room", "cage"],
        };
        var candidates = kinds.Where(_housing.ContainsKey).SelectMany(k => _housing[k]).ToList();
        if (candidates.Count == 0)
        {
            candidates = [.. _housing.Where(h => h.Key is not ("external_clinic" or "field")).SelectMany(h => h.Value)];
        }

        return candidates.Count == 0 ? _context.Pick(_housing.Values.First()) : _context.Pick(candidates);
    }

    private string? Note(double probability) =>
        _context.Chance(probability) ? _context.Pick(SampleData.MovementNotes) : null;

    private DateTimeOffset Clamp(DateTimeOffset value) => value < _limit ? value : _limit;

    private Guid Expect(Results<Created<MovementItem>, ValidationProblem, ProblemHttpResult> result, string item)
    {
        if (result.Result is not Created<MovementItem> { Value: { } movement })
        {
            throw new InvalidOperationException($"Dev seed could not record {item}: " + Describe(result.Result));
        }

        _context.Count("movements." + item.Split('.')[0]);
        return movement.Id;
    }

    private static string Describe(object result) => result switch
    {
        ValidationProblem v => string.Join("; ", v.ProblemDetails.Errors.Select(e => e.Key + ": " + string.Join(", ", e.Value))),
        ProblemHttpResult p => p.ProblemDetails.Detail ?? p.ProblemDetails.Title ?? "problem",
        _ => result.GetType().Name,
    };

    private static Dictionary<string, List<Guid>> Group(IReadOnlyList<DevSeedRecord> records) =>
        records.GroupBy(r => r.Tag, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToList(), StringComparer.Ordinal);
}
