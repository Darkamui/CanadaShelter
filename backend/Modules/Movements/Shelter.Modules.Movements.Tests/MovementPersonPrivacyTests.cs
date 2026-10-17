using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Movements.Domain;
using Shelter.Modules.Movements.Features.Movements;
using Shelter.Modules.Operations.Contracts;
using Shelter.Modules.People.Contracts;
using Shelter.Testing;

namespace Shelter.Modules.Movements.Tests;

/// <summary>
/// M3-5: a person ID links an animal to a person, so recording one needs <c>person.read</c>, and without it the history
/// leaves out the person ID, name and notes. Every built-in role has <c>person.read</c> today, so the recorder is called
/// directly. Tenant context, user, custody and timeline are not reached on these paths and are left null.
/// </summary>
public sealed class MovementPersonPrivacyTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Recording_an_intake_with_a_person_is_forbidden_without_person_read()
    {
        var people = new FakePeople(Guid.CreateVersion7());

        var result = await WithRecorderAsync(people, (recorder, ct) => recorder.IntakeAsync(
            new RecordIntakeRequest(Guid.CreateVersion7(), "stray", Guid.CreateVersion7(), people.PersonId, null, null), ct));

        AssertForbidden(result.Result);
        Assert.False(people.WasCalled);
    }

    [Fact]
    public async Task Recording_an_outcome_with_a_person_is_forbidden_without_person_read()
    {
        var people = new FakePeople(Guid.CreateVersion7());

        var result = await WithRecorderAsync(people, (recorder, ct) => recorder.OutcomeAsync(
            new RecordOutcomeRequest(Guid.CreateVersion7(), "adoption", people.PersonId, null, null), ct));

        AssertForbidden(result.Result);
        Assert.False(people.WasCalled);
    }

    [Fact]
    public async Task History_leaves_out_the_person_and_notes_without_person_read()
    {
        var item = await SeededItemAsync(MovementPermissions());

        Assert.Null(item.PersonId);
        Assert.Null(item.PersonName);
        Assert.Null(item.Notes);
        Assert.Equal("Enclos 4", item.ToLocationName);
    }

    [Fact]
    public async Task History_shows_the_person_and_notes_with_person_read()
    {
        var item = await SeededItemAsync([.. MovementPermissions(), PeoplePermissionNames.Read]);

        Assert.NotNull(item.PersonId);
        Assert.Equal("Marie Tremblay", item.PersonName);
        Assert.Equal("Trouvé près du parc", item.Notes);
    }

    // movement.read and movement.write, without person.read.
    private static string[] MovementPermissions() => ["movement.read", "movement.write"];

    private async Task<MovementItem> SeededItemAsync(string[] granted)
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        var people = new FakePeople(Guid.CreateVersion7());
        var movement = await fixture.Tenants.SeedAsync(
            tenant,
            Movement.Intake(Guid.CreateVersion7(), "stray", FakeLocations.KennelId, people.PersonId, "Trouvé près du parc", Now, Now, Guid.CreateVersion7()),
            ct);

        var items = await fixture.Tenants.InTenantAsync(tenant, db =>
            NewRecorder(db, people, granted).ToItemsAsync([movement], ct));
        Assert.Equal(granted.Contains(PeoplePermissionNames.Read), people.WasCalled);
        return Assert.Single(items);
    }

    private Task<T> WithRecorderAsync<T>(FakePeople people, Func<MovementRecorder, CancellationToken, Task<T>> work) =>
        fixture.Tenants.InTenantAsync(TenantHarness.NewTenantId(), db =>
            work(NewRecorder(db, people, MovementPermissions()), TestContext.Current.CancellationToken));

    private static MovementRecorder NewRecorder(ShelterDbContext db, FakePeople people, string[] granted) =>
        new(db, null!, null!, new FakePermissions(granted), null!, null!, new FakeLocations(), people, TimeProvider.System);

    private static void AssertForbidden(object result) =>
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result).StatusCode);

    private sealed class FakePermissions(params string[] granted) : IPermissionContext
    {
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(granted);

        public bool Has(string permission) => Permissions.Contains(permission);
    }

    private sealed class FakeLocations : ILocationDirectory
    {
        public static readonly Guid KennelId = Guid.CreateVersion7();

        public Task<IReadOnlyDictionary<Guid, LocationSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, LocationSummary>>(
                new Dictionary<Guid, LocationSummary> { [KennelId] = new(KennelId, null, "Enclos 4", "kennel", false) });

        public Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid rootId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([rootId]);

        public Task<bool> LockForPlacementAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakePeople(Guid personId) : IPersonDirectory
    {
        public Guid PersonId { get; } = personId;

        public bool WasCalled { get; private set; }

        public Task<IReadOnlyDictionary<Guid, PersonSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyDictionary<Guid, PersonSummary>>(
                new Dictionary<Guid, PersonSummary> { [PersonId] = new(PersonId, "Marie Tremblay", false) });
        }

        public Task<bool> ExistsActiveAsync(Guid id, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(true);
        }
    }
}
