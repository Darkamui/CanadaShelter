using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence.Paging;
using Shelter.Modules.Animals.Authorization;
using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Animals.Domain;
using Shelter.Modules.Animals.Features.Animals;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.People.Contracts;
using Shelter.Testing;

namespace Shelter.Modules.Animals.Tests;

/// <summary>
/// M3-4: the timeline holds IDs and codes only, rejects anything else on append, and shows person IDs and names only to
/// callers with <c>person.read</c>. Every built-in role has <c>person.read</c> today, so the handler is called directly.
/// </summary>
public sealed class AnimalTimelineTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Timeline_hides_person_ids_and_names_without_person_read()
    {
        var (tenant, animalId, personId) = await SeedIntakeAsync();
        var people = new FakePeople(personId, "Marie Tremblay");

        var item = await FirstItemAsync(tenant, animalId, people, AnimalPermissions.Read);

        Assert.False(item.Parameters.ContainsKey("personId"));
        Assert.False(item.Names.ContainsKey("personId"));
        Assert.Equal("stray", item.Parameters["reasonCode"]);
        Assert.Equal("Enclos 4", item.Names["toLocationId"]);
        Assert.False(people.WasCalled);
    }

    [Fact]
    public async Task Timeline_shows_person_ids_and_names_with_person_read()
    {
        var (tenant, animalId, personId) = await SeedIntakeAsync();

        var item = await FirstItemAsync(
            tenant, animalId, new FakePeople(personId, "Marie Tremblay"), AnimalPermissions.Read, PeoplePermissionNames.Read);

        Assert.Equal(personId.ToString(), item.Parameters["personId"]);
        Assert.Equal("Marie Tremblay", item.Names["personId"]);
    }

    [Theory]
    [InlineData("note", "Mordu Mme Tremblay")]
    [InlineData("person name", "abc")]
    [InlineData("reasonCode", "Stray")]
    [InlineData("reasonCode", "")]
    public async Task Append_refuses_anything_but_ids_and_codes(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(), ct);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.InTenantAsync(tenant, db =>
        {
            new AnimalTimeline(db, TimeProvider.System).Append(new TimelineEntry(
                animal.Id, "intake_recorded", Now, "movements", null, new Dictionary<string, string> { [key] = value }));
            return Task.FromResult(0);
        }));
    }

    [Fact]
    public async Task Append_for_another_tenants_animal_is_refused_by_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var theirs = await fixture.Tenants.SeedAsync(TenantHarness.NewTenantId(), NewAnimal(), ct);

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.InTenantAsync(TenantHarness.NewTenantId(), db =>
        {
            new AnimalTimeline(db, TimeProvider.System).Append(new TimelineEntry(theirs.Id, "intake_recorded", Now, "movements", null));
            return db.SaveChangesAsync(ct);
        }));
    }

    private async Task<(Guid Tenant, Guid AnimalId, Guid PersonId)> SeedIntakeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var animal = await fixture.Tenants.SeedAsync(tenant, NewAnimal(), ct);
        var personId = Guid.CreateVersion7();
        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["reasonCode"] = "stray",
            ["toLocationId"] = FakeLocations.KennelId.ToString(),
            ["personId"] = personId.ToString(),
        });
        await fixture.Tenants.SeedAsync(tenant, new TimelineEvent(animal.Id, "intake_recorded", Now, Now, "movements", null, parameters), ct);
        return (tenant, animal.Id, personId);
    }

    private Task<TimelineItem> FirstItemAsync(Guid tenant, Guid animalId, FakePeople people, params string[] granted) =>
        fixture.InTenantAsync(tenant, async db =>
        {
            var result = await AnimalEndpoints.Timeline(
                animalId, null, null, db, new FakeLocations(), people, new FakePermissions(granted), TestContext.Current.CancellationToken);
            var page = Assert.IsType<Ok<PagedResult<TimelineItem>>>(result.Result).Value!;
            return Assert.Single(page.Items);
        });

    private static Animal NewAnimal() =>
        new(1, new AnimalDetails("Rex", "dog", null, null, null, AnimalSexes.Unknown, ReproductiveStatuses.Unknown, null, false, null, null, null, null), Now);

    private sealed class FakePermissions(params string[] granted) : IPermissionContext
    {
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(granted);

        public bool Has(string permission) => Permissions.Contains(permission);
    }

    private sealed class FakeLocations : ILocationDirectory
    {
        public static readonly Guid KennelId = Guid.CreateVersion7();

        public Task<IReadOnlyDictionary<Guid, LocationSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, LocationSummary>>(ids.Contains(KennelId)
                ? new Dictionary<Guid, LocationSummary> { [KennelId] = new(KennelId, null, "Enclos 4", "kennel", false) }
                : []);

        public Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid rootId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([rootId]);

        public Task<bool> IsActiveHoldingAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakePeople(Guid personId, string displayName) : IPersonDirectory
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyDictionary<Guid, PersonSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyDictionary<Guid, PersonSummary>>(
                new Dictionary<Guid, PersonSummary> { [personId] = new(personId, displayName, false) });
        }

        public Task<bool> ExistsActiveAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
