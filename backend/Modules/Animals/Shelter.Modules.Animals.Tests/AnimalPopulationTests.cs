using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shelter.BuildingBlocks.Authorization;
using Shelter.Modules.Animals.Authorization;
using Shelter.Modules.Animals.Features.Animals;
using Shelter.Modules.Operations.Contracts;
using Shelter.Testing;

namespace Shelter.Modules.Animals.Tests;

/// <summary>
/// M3-6: the population rows reveal the shape of the location tree, so they need <c>location.read</c> as well as
/// <c>animal.read</c>. Every built-in role has both today, so the handler is called directly. The counts themselves are
/// covered by <c>MovementEndpointTests.Population_counts_animals_in_care_per_location_and_per_subtree</c>.
/// </summary>
public sealed class AnimalPopulationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Population_is_forbidden_without_location_read()
    {
        var locations = new NoLocations();

        var result = await PopulationAsync(locations, AnimalPermissions.Read);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.False(locations.WasCalled);
    }

    [Fact]
    public async Task Population_is_returned_with_location_read()
    {
        var result = await PopulationAsync(new NoLocations(), AnimalPermissions.Read, OperationsPermissionNames.LocationRead);

        Assert.IsType<Ok<IReadOnlyList<LocationPopulationItem>>>(result.Result);
    }

    private Task<Results<Ok<IReadOnlyList<LocationPopulationItem>>, ProblemHttpResult>> PopulationAsync(
        ILocationDirectory locations, params string[] granted) =>
        fixture.InTenantAsync(TenantHarness.NewTenantId(), db =>
            AnimalEndpoints.Population(db, locations, new FakePermissions(granted), TestContext.Current.CancellationToken));

    private sealed class FakePermissions(params string[] granted) : IPermissionContext
    {
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(granted);

        public bool Has(string permission) => Permissions.Contains(permission);
    }

    private sealed class NoLocations : ILocationDirectory
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyDictionary<Guid, LocationSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyDictionary<Guid, LocationSummary>>(new Dictionary<Guid, LocationSummary>());
        }

        public Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid rootId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([rootId]);

        public Task<bool> LockForPlacementAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
