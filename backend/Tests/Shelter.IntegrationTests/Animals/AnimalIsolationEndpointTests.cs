using System.Net;
using System.Net.Http.Json;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Animals;

/// <summary>
/// M3-4: animals, their identifiers and their timeline never cross a tenant boundary through the HTTP surface, the
/// caller's tenant cannot be overridden from the request body, numbering and microchip uniqueness are per tenant,
/// and role-scoped permissions are enforced.
/// </summary>
public sealed class AnimalIsolationEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string AnimalsPath = "/api/animals";
    private const string MicrochipA = "985112345678901";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organizationA = Guid.CreateVersion7();
    private readonly Guid _organizationB = Guid.CreateVersion7();

    [Fact]
    public async Task List_from_other_tenant_never_shows_the_animal()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog", microchip = MicrochipA });
        await CreateAsync(clientB, new { name = "Biscuit", speciesCode = "dog" });

        var all = await ListAsync(clientB, string.Empty);

        Assert.DoesNotContain(created.Id, all.Items.Select(a => a.Id));
        Assert.Equal(1, all.TotalCount);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("number")]
    [InlineData("numberWithHash")]
    [InlineData("microchip")]
    public async Task List_search_from_other_tenant_never_finds_the_animal(string searchBy)
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog", microchip = MicrochipA });
        var q = searchBy switch
        {
            "name" => "Pistache",
            "number" => created.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "numberWithHash" => "#" + created.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => MicrochipA,
        };

        var foundByB = await ListAsync(clientB, $"q={Uri.EscapeDataString(q)}");
        var foundByA = await ListAsync(clientA, $"q={Uri.EscapeDataString(q)}");

        Assert.Empty(foundByB.Items);
        Assert.Contains(created.Id, foundByA.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Get_animal_from_other_tenant_returns_404()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog" });

        using var response = await clientB.GetAsync(Url($"{created.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_timeline_of_animal_from_other_tenant_returns_404()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog" });

        using var own = await clientA.GetAsync(Url($"{created.Id}/timeline"), TestContext.Current.CancellationToken);
        using var foreign = await clientB.GetAsync(Url($"{created.Id}/timeline"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Update_animal_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Original", speciesCode = "dog" });

        using var update = await clientB.PutAsJsonAsync(
            Url($"{created.Id}"),
            new { name = "Modifié", speciesCode = "dog", version = created.Version },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        var after = await GetAsync(clientA, created.Id);
        Assert.Equal("Original", after.Name);
        Assert.Equal(created.Version, after.Version);
    }

    [Fact]
    public async Task Add_identifier_to_animal_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog" });

        using var add = await clientB.PostAsJsonAsync(
            Url($"{created.Id}/identifiers"), new { type = "licence", value = "LIC-42" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        var after = await GetAsync(clientA, created.Id);
        Assert.Empty(after.Identifiers);
        Assert.Equal(created.Version, after.Version);
    }

    [Fact]
    public async Task Deactivate_identifier_from_other_tenant_returns_404_and_leaves_it_active()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog", microchip = MicrochipA });
        var identifier = created.Identifiers.Single();

        var status = await DeactivateAsync(clientB, created.Id, identifier.Id);

        Assert.Equal(HttpStatusCode.NotFound, status);
        var after = await GetAsync(clientA, created.Id);
        Assert.True(after.Identifiers.Single().IsActive);
        Assert.Equal(created.Version, after.Version);
    }

    [Fact]
    public async Task Deactivate_identifier_through_own_animal_id_and_other_tenants_identifier_returns_404()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var inA = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog", microchip = MicrochipA });
        var inB = await CreateAsync(clientB, new { name = "Biscuit", speciesCode = "dog" });

        var status = await DeactivateAsync(clientB, inB.Id, inA.Identifiers.Single().Id);

        Assert.Equal(HttpStatusCode.NotFound, status);
        var after = await GetAsync(clientA, inA.Id);
        Assert.True(after.Identifiers.Single().IsActive);
    }

    [Fact]
    public async Task Create_ignores_a_tenant_id_from_the_request_body()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);

        var created = await CreateAsync(clientB, new
        {
            name = "Corps étranger",
            speciesCode = "dog",
            tenantId = _organizationA,
            organizationId = _organizationA,
        });

        var seenByB = await ListAsync(clientB, string.Empty);
        var seenByA = await ListAsync(clientA, string.Empty);
        Assert.Contains(created.Id, seenByB.Items.Select(a => a.Id));
        Assert.DoesNotContain(created.Id, seenByA.Items.Select(a => a.Id));
        using var direct = await clientA.GetAsync(Url($"{created.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    [Fact]
    public async Task First_animal_of_each_tenant_gets_number_1()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);

        var inA = await CreateAsync(clientA, new { name = "Pistache", speciesCode = "dog" });
        var inB = await CreateAsync(clientB, new { name = "Biscuit", speciesCode = "dog" });
        var secondInA = await CreateAsync(clientA, new { name = "Noisette", speciesCode = "dog" });

        Assert.Equal(1, inA.Number);
        Assert.Equal(1, inB.Number);
        Assert.Equal(2, secondInA.Number);
    }

    [Fact]
    public async Task Same_microchip_in_two_tenants_is_allowed()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);

        using var first = await clientA.PostAsJsonAsync(
            Url(string.Empty), new { name = "Pistache", speciesCode = "dog", microchip = MicrochipA }, TestContext.Current.CancellationToken);
        using var second = await clientB.PostAsJsonAsync(
            Url(string.Empty), new { name = "Biscuit", speciesCode = "dog", microchip = MicrochipA }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task List_filtered_by_a_location_unknown_to_the_caller_returns_no_rows()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        await CreateAsync(clientB, new { name = "Biscuit", speciesCode = "dog" });
        var locationInA = await CreateLocationAsync(clientA, "Pavillon A");

        var foreign = await ListAsync(clientB, $"locationId={locationInA}");
        var random = await ListAsync(clientB, $"locationId={Guid.CreateVersion7()}");

        Assert.Empty(foreign.Items);
        Assert.Equal(0, foreign.TotalCount);
        Assert.Empty(random.Items);
    }

    [Fact]
    public async Task Read_only_may_list_animals_but_not_create_them()
    {
        using var staff = await StaffClientAsync(_organizationA);
        var created = await CreateAsync(staff, new { name = "Lecture seule", speciesCode = "dog" });

        var readOnlyUser = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organizationA, SystemRoles.ReadOnly);
        using var readOnly = await _factory.CreateAntiforgeryClientAsync(readOnlyUser, _organizationA);

        using var list = await readOnly.GetAsync(Url(string.Empty), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var create = await readOnly.PostAsJsonAsync(
            Url(string.Empty), new { name = "Jamais", speciesCode = "dog" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var update = await readOnly.PutAsJsonAsync(
            Url($"{created.Id}"), new { name = "Jamais", speciesCode = "dog", version = created.Version }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        using var add = await readOnly.PostAsJsonAsync(
            Url($"{created.Id}/identifiers"), new { type = "licence", value = "LIC-1" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static Uri Url(string suffix) =>
        new(suffix.Length == 0 ? AnimalsPath : $"{AnimalsPath}/{suffix}", UriKind.Relative);

    private async Task<HttpClient> StaffClientAsync(Guid organizationId)
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organizationId, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, organizationId);
    }

    private static async Task<AnimalDto> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(Url(string.Empty), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AnimalDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<AnimalDto> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AnimalDto>(Url($"{id}"), TestContext.Current.CancellationToken))!;

    private static async Task<PageDto> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PageDto>(new Uri($"{AnimalsPath}?{query}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<HttpStatusCode> DeactivateAsync(HttpClient client, Guid animalId, Guid identifierId)
    {
        using var response = await client.PostAsync(
            Url($"{animalId}/identifiers/{identifierId}/deactivate"), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<Guid> CreateLocationAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/operations/locations", UriKind.Relative),
            new { kindCode = "building", name },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationDto>(TestContext.Current.CancellationToken))!.Id;
    }

    private sealed record IdentifierDto(Guid Id, string Type, string Value, bool IsActive);

    private sealed record AnimalDto(Guid Id, int Number, string? Name, List<IdentifierDto> Identifiers, uint Version);

    private sealed record ListItemDto(Guid Id, int Number, string? Name);

    private sealed record PageDto(List<ListItemDto> Items, int Page, int PageSize, int TotalCount);

    private sealed record LocationDto(Guid Id);
}
