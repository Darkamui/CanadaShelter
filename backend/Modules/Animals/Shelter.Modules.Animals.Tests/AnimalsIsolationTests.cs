using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;
using Shelter.Modules.Animals.Features.Animals;
using Shelter.Modules.Animals.Features.ReferenceData;
using Shelter.Testing;

namespace Shelter.Modules.Animals.Tests;

/// <summary>
/// M3-4: animals, identifiers, number counters, timeline events and species overrides are tenant-isolated; the species
/// list merges only the caller's overrides, and the contracts (ADR 0021) see only the caller's organization.
/// </summary>
public sealed class AnimalsIsolationTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public Task Animal_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => NewAnimal(1, "Éclair"), TestContext.Current.CancellationToken);

    [Fact]
    public Task Animal_identifier_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            async tenant =>
            {
                var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(1, "Éclair"), TestContext.Current.CancellationToken);
                return new AnimalIdentifier(animal.Id, AnimalIdentifierTypes.Microchip, "900 123 456 789 012", Now);
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task Animal_number_counter_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => new AnimalNumberCounter(7), TestContext.Current.CancellationToken);

    [Fact]
    public Task Timeline_event_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            async tenant =>
            {
                var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(1, "Éclair"), TestContext.Current.CancellationToken);
                return new TimelineEvent(animal.Id, AnimalTimelineTypes.Registered, Now, Now, "animals", null, "{}");
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task Species_override_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            () => new SpeciesOverride("alpaca", new LocalizedText("Alpaga", "Alpaca"), 40, isHidden: false),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Species_catalog_applies_only_the_callers_overrides()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = TenantHarness.NewTenantId();
        var tenantB = TenantHarness.NewTenantId();
        await fixture.Tenants.SeedAsync(tenantA, new SpeciesOverride("dog", new LocalizedText("Chien", "Dog"), 10, isHidden: true), ct);
        await fixture.Tenants.SeedAsync(tenantA, new SpeciesOverride("alpaca", new LocalizedText("Alpaga", "Alpaca"), 40, isHidden: false), ct);

        var speciesA = await fixture.InTenantAsync(tenantA, db => SpeciesCatalog.LoadAsync(db, Tenant(tenantA), ct));
        var speciesB = await fixture.InTenantAsync(tenantB, db => SpeciesCatalog.LoadAsync(db, Tenant(tenantB), ct));

        Assert.True(speciesA["dog"].IsHidden);
        Assert.False(SpeciesCatalog.IsVisible(speciesA, "dog"));
        Assert.True(SpeciesCatalog.IsVisible(speciesA, "alpaca"));

        Assert.True(SpeciesCatalog.IsVisible(speciesB, "dog"));
        Assert.DoesNotContain("alpaca", speciesB.Keys);
        Assert.False(SpeciesCatalog.IsVisible(speciesB, "unknown-code"));
    }

    [Fact]
    public async Task Population_counts_only_the_callers_animals_in_care()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = TenantHarness.NewTenantId();
        var tenantB = TenantHarness.NewTenantId();
        var room = Guid.CreateVersion7();
        var kennel = Guid.CreateVersion7();
        await fixture.Tenants.SeedAsync(tenantA, InCareAt(NewAnimal(1, "Un"), room), ct);
        await fixture.Tenants.SeedAsync(tenantA, InCareAt(NewAnimal(2, "Deux"), room), ct);
        await fixture.Tenants.SeedAsync(tenantA, InCareAt(NewAnimal(3, "Trois"), kennel), ct);
        await fixture.Tenants.SeedAsync(tenantA, NewAnimal(4, "Pas là"), ct);
        await fixture.Tenants.SeedAsync(tenantB, InCareAt(NewAnimal(1, "Leur animal"), room), ct);

        var counts = await fixture.InTenantAsync(tenantA, db => new AnimalPopulation(db).CountAtAsync([room, kennel, Guid.CreateVersion7()], ct));
        var none = await fixture.InTenantAsync(tenantA, db => new AnimalPopulation(db).CountAtAsync([], ct));

        Assert.Equal(2, counts[room]);
        Assert.Equal(1, counts[kennel]);
        Assert.Equal(2, counts.Count);
        Assert.Empty(none);
    }

    [Fact]
    public async Task Custody_lock_does_not_find_another_tenants_animal()
    {
        var ct = TestContext.Current.CancellationToken;
        var theirs = await fixture.Tenants.SeedAsync(TenantHarness.NewTenantId(), NewAnimal(1, "Leur animal"), ct);

        var state = await fixture.InTenantAsync(TenantHarness.NewTenantId(), db => new AnimalCustody(db, TimeProvider.System).LockAsync(theirs.Id, ct));

        Assert.Null(state);
    }

    [Fact]
    public async Task Custody_apply_replaces_the_summary_when_it_is_still_the_expected_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(1, "Éclair"), ct);
        var location = Guid.CreateVersion7();
        var intake = Guid.CreateVersion7();
        var inCare = new AnimalCustodyState(CustodyStatuses.InCare, location, Now, intake, null);

        await fixture.InTenantAsync(tenant, async db =>
        {
            var custody = new AnimalCustody(db, TimeProvider.System);
            var current = await custody.LockAsync(animal.Id, ct);
            Assert.Equal(AnimalCustodyState.NotInCare, current);
            custody.Apply(animal.Id, current!, inCare);
            return await db.SaveChangesAsync(ct);
        });

        var stored = await fixture.InTenantAsync(tenant, db => new AnimalCustody(db, TimeProvider.System).LockAsync(animal.Id, ct));
        Assert.Equal(CustodyStatuses.InCare, stored!.Status);
        Assert.Equal(location, stored.LocationId);
        Assert.Equal(intake, stored.CurrentIntakeId);
    }

    [Fact]
    public async Task Custody_apply_refuses_a_summary_that_is_not_the_expected_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(1, "Éclair"), ct);
        var stale = new AnimalCustodyState(CustodyStatuses.InCare, Guid.CreateVersion7(), Now, Guid.CreateVersion7(), null);

        await Assert.ThrowsAsync<AnimalCustodyConflictException>(() => fixture.InTenantAsync(tenant, async db =>
        {
            var custody = new AnimalCustody(db, TimeProvider.System);
            await custody.LockAsync(animal.Id, ct);
            custody.Apply(animal.Id, stale, AnimalCustodyState.NotInCare with { LastOutcomeCode = "adoption", Status = CustodyStatuses.Outcome });
            return 0;
        }));
    }

    [Fact]
    public async Task Custody_apply_requires_the_animal_to_be_locked_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(1, "Éclair"), ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InTenantAsync(tenant, db =>
        {
            new AnimalCustody(db, TimeProvider.System).Apply(animal.Id, AnimalCustodyState.NotInCare, AnimalCustodyState.NotInCare);
            return Task.FromResult(0);
        }));
    }

    private static Animal NewAnimal(int number, string name) =>
        new(number, new AnimalDetails(name, "dog", null, null, null, AnimalSexes.Unknown, ReproductiveStatuses.Unknown, null, false, null, null, null, null), Now);

    private static Animal InCareAt(Animal animal, Guid locationId)
    {
        animal.ApplyCustody(new AnimalCustodyState(CustodyStatuses.InCare, locationId, Now, Guid.CreateVersion7(), null), Now);
        return animal;
    }

    private static TenantContext Tenant(Guid tenantId)
    {
        var context = new TenantContext();
        context.Set(tenantId);
        return context;
    }
}
