using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Operations.Domain;
using Shelter.Modules.Operations.Features.Locations;
using Shelter.Testing;

namespace Shelter.Modules.Operations.Tests;

/// <summary>
/// M3-3: locations and location kind overrides are tenant-isolated; the kind list merges only the caller's overrides,
/// and the directory contract sees only the caller's organization.
/// </summary>
public sealed class OperationsIsolationTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public Task Location_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => new Location(null, "shelter", "Refuge", null, Now), TestContext.Current.CancellationToken);

    [Fact]
    public Task Location_kind_override_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            () => new LocationKindOverride("barn", new LocalizedText("Grange", "Barn"), 15, isHidden: false, holdsAnimals: true),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Kind_catalog_applies_only_the_callers_overrides()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = TenantHarness.NewTenantId();
        var tenantB = TenantHarness.NewTenantId();
        await fixture.Tenants.SeedAsync(tenantA, new LocationKindOverride("cage", new LocalizedText("Cage", "Cage"), 50, isHidden: true, holdsAnimals: true), ct);
        await fixture.Tenants.SeedAsync(tenantA, new LocationKindOverride("building", new LocalizedText("Pavillon", "Pavilion"), 20, isHidden: false, holdsAnimals: true), ct);
        await fixture.Tenants.SeedAsync(tenantA, new LocationKindOverride("barn", new LocalizedText("Grange", "Barn"), 15, isHidden: false, holdsAnimals: true), ct);

        var kindsA = await fixture.InTenantAsync(tenantA, db => LocationKindCatalog.LoadAsync(db, Tenant(tenantA), ct));
        var kindsB = await fixture.InTenantAsync(tenantB, db => LocationKindCatalog.LoadAsync(db, Tenant(tenantB), ct));

        Assert.True(kindsA["cage"].IsHidden);
        Assert.DoesNotContain(LocationKindCatalog.Visible(kindsA), k => k.Code == "cage");
        Assert.Equal("Pavillon", kindsA["building"].Label.Fr);
        Assert.True(kindsA["building"].HoldsAnimals);
        Assert.Contains("barn", kindsA.Keys);
        Assert.Equal(["shelter", "barn", "building"], LocationKindCatalog.Visible(kindsA).Take(3).Select(k => k.Code));

        Assert.Equal(10, kindsB.Count);
        Assert.False(kindsB["cage"].IsHidden);
        Assert.False(kindsB["building"].HoldsAnimals);
        Assert.DoesNotContain("barn", kindsB.Keys);
    }

    [Fact]
    public async Task Directory_resolves_only_the_callers_locations()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = TenantHarness.NewTenantId();
        var tenantB = TenantHarness.NewTenantId();
        var building = await fixture.Tenants.SeedAsync(tenantA, new Location(null, "building", "Pavillon A", null, Now), ct);
        var room = await fixture.Tenants.SeedAsync(tenantA, new Location(building.Id, "room", "Salle 1", 4, Now), ct);
        var kennel = await fixture.Tenants.SeedAsync(tenantA, new Location(room.Id, "kennel", "Enclos 1", 1, Now), ct);
        var archived = new Location(building.Id, "room", "Ancienne salle", null, Now);
        archived.Archive(Now);
        archived = await fixture.Tenants.SeedAsync(tenantA, archived, ct);
        var theirs = await fixture.Tenants.SeedAsync(tenantB, new Location(null, "room", "Leur salle", null, Now), ct);

        var result = await fixture.InTenantAsync(tenantA, async db =>
        {
            var directory = new LocationDirectory(db, Tenant(tenantA));
            return (
                Summaries: await directory.GetAsync([room.Id, theirs.Id, room.Id], ct),
                Subtree: await directory.GetSubtreeIdsAsync(building.Id, ct),
                TheirSubtree: await directory.GetSubtreeIdsAsync(theirs.Id, ct),
                RoomHolds: await directory.LockForPlacementAsync(room.Id, ct),
                BuildingHolds: await directory.LockForPlacementAsync(building.Id, ct),
                ArchivedHolds: await directory.LockForPlacementAsync(archived.Id, ct),
                TheirsHolds: await directory.LockForPlacementAsync(theirs.Id, ct));
        });

        Assert.Equal([room.Id], result.Summaries.Keys);
        Assert.Equal(building.Id, result.Summaries[room.Id].ParentId);
        Assert.Equal(
            new HashSet<Guid> { building.Id, room.Id, kennel.Id, archived.Id },
            result.Subtree.ToHashSet());
        Assert.Empty(result.TheirSubtree);
        Assert.True(result.RoomHolds);
        Assert.False(result.BuildingHolds);
        Assert.False(result.ArchivedHolds);
        Assert.False(result.TheirsHolds);
    }

    [Fact]
    public async Task Hidden_kind_keeps_its_holds_animals_attribute()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var cage = await fixture.Tenants.SeedAsync(tenant, new Location(null, "cage", "Cage 1", 1, Now), ct);
        await fixture.Tenants.SeedAsync(tenant, new LocationKindOverride("cage", new LocalizedText("Cage", "Cage"), 50, isHidden: true, holdsAnimals: true), ct);

        var holds = await fixture.InTenantAsync(tenant, db => new LocationDirectory(db, Tenant(tenant)).LockForPlacementAsync(cage.Id, ct));

        Assert.True(holds);
    }

    private static TenantContext Tenant(Guid tenantId)
    {
        var context = new TenantContext();
        context.Set(tenantId);
        return context;
    }
}
