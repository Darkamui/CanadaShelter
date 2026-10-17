using System.Globalization;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;
using Shelter.Modules.Animals.Features.Animals;
using Shelter.Modules.Animals.Features.ReferenceData;

namespace Shelter.Modules.Animals.Features.DevSeed;

/// <summary>
/// Development seed (ADR 0023): 25 animals per factor unit, through the same validation, number counter and timeline
/// as the API, registered at backdated times in number order. About a third are registered in the last 60 days, so
/// that many are still in care once Movements has run. Mostly cats and dogs, with identifiers and some alerts. The
/// animals stay not in care here: Movements takes them in.
/// </summary>
internal sealed class AnimalsDevSeed(ShelterDbContext db, ITenantContext tenant, IAnimalTimeline timeline) : IDevSeedContributor
{
    private const int BatchSize = 200;
    private const string SourceModule = "animals";

    private static readonly (string Code, int Weight)[] SpeciesWeights =
    [
        ("cat", 45), ("dog", 40), ("rabbit", 6), ("small_mammal", 4), ("bird", 2), ("ferret", 1), ("reptile", 1),
    ];

    public int Order => 30;

    public async Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = tenant.RequireTenantId();
        var catalog = await SpeciesCatalog.LoadAsync(db, tenant, cancellationToken);
        var species = SpeciesWeights.Where(s => SpeciesCatalog.IsVisible(catalog, s.Code)).ToList();
        if (species.Count == 0)
        {
            species = [.. catalog.Values.Where(s => !s.IsHidden).Select(s => (s.Code, 1))];
        }

        // Registration times first, sorted, so animal numbers follow the history.
        var count = context.Scaled(25);
        var registrations = Enumerable.Range(0, count)
            .Select(_ => context.Chance(0.35)
                ? context.Between(context.Now.AddDays(-60), context.Now.AddHours(-2))
                : context.Between(context.Start, context.Now.AddDays(-60)))
            .Order()
            .ToList();

        var saved = 0;
        foreach (var registeredAt in registrations)
        {
            var speciesCode = context.Weighted(species);
            var (details, errors) = AnimalRequestValidator.Validate(
                Request(context, speciesCode, registeredAt), DateOnly.FromDateTime(context.Now.UtcDateTime));
            if (details is null)
            {
                throw new InvalidOperationException("Dev seed generated an invalid animal: " + string.Join(", ", errors.Keys));
            }

            var number = await AnimalEndpoints.NextNumberAsync(db, tenantId, cancellationToken);
            var animal = new Animal(number, details, registeredAt);
            db.Set<Animal>().Add(animal);
            AddIdentifiers(context, animal, registeredAt);
            timeline.Append(new TimelineEntry(animal.Id, AnimalTimelineTypes.Registered, registeredAt, SourceModule, animal.Id));
            context.Publish(DevSeedKeys.Animals, new DevSeedRecord(animal.Id, registeredAt, speciesCode));
            context.Count("animals");

            if (++saved % BatchSize == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static CreateAnimalRequest Request(DevSeedContext context, string speciesCode, DateTimeOffset registeredAt)
    {
        var (names, breeds) = speciesCode switch
        {
            "dog" => (SampleData.DogNames, SampleData.DogBreeds),
            "cat" => (SampleData.CatNames, SampleData.CatBreeds),
            _ => (SampleData.SmallAnimalNames, (IReadOnlyList<string>?)null),
        };

        // Kittens and puppies are common intakes; adults up to 14 years.
        var ageDays = context.Chance(0.3) ? context.Random.Next(30, 300) : context.Random.Next(300, 14 * 365);
        var birthDate = DateOnly.FromDateTime(registeredAt.UtcDateTime.AddDays(-ageDays));
        return new CreateAnimalRequest(
            Name: context.Chance(0.9) ? context.Pick(names) : null,
            SpeciesCode: speciesCode,
            Breed: breeds is not null && context.Chance(0.8) ? context.Pick(breeds) : null,
            SecondaryBreed: breeds is not null && context.Chance(0.15) ? context.Pick(breeds) : null,
            Colour: context.Chance(0.9) ? context.Pick(SampleData.Colours) : null,
            Sex: context.Weighted([(AnimalSexes.Female, 48), (AnimalSexes.Male, 48), (AnimalSexes.Unknown, 4)]),
            ReproductiveStatus: context.Weighted(
                [(ReproductiveStatuses.Intact, 45), (ReproductiveStatuses.Sterilized, 45), (ReproductiveStatuses.Unknown, 10)]),
            BirthDate: context.Chance(0.85) ? birthDate : null,
            BirthDateEstimated: context.Chance(0.6),
            Marks: context.Chance(0.15) ? context.Pick(SampleData.Marks) : null,
            BehaviourAlert: context.Chance(0.08) ? context.Pick(SampleData.BehaviourAlerts) : null,
            MedicalAlert: context.Chance(0.07) ? context.Pick(SampleData.MedicalAlerts) : null,
            LegalAlert: context.Chance(0.02) ? context.Pick(SampleData.LegalAlerts) : null,
            Microchip: null);
    }

    private void AddIdentifiers(DevSeedContext context, Animal animal, DateTimeOffset registeredAt)
    {
        var dogOrCat = animal.SpeciesCode is "dog" or "cat";
        if (dogOrCat && context.Chance(0.55))
        {
            Add(AnimalIdentifierTypes.Microchip, SampleData.Microchip(animal.Number));
        }

        if (animal.SpeciesCode == "dog" && context.Chance(0.2))
        {
            Add(AnimalIdentifierTypes.Licence, string.Create(CultureInfo.InvariantCulture, $"MTL-{registeredAt.Year}-{animal.Number:D5}"));
        }

        if (context.Chance(0.08))
        {
            Add(AnimalIdentifierTypes.External, string.Create(CultureInfo.InvariantCulture, $"TR-{context.Random.Next(1000, 99999)}"));
        }

        void Add(string type, string value)
        {
            db.Set<AnimalIdentifier>().Add(new AnimalIdentifier(animal.Id, type, value, registeredAt));
            context.Count("animals.identifiers");
        }
    }
}
