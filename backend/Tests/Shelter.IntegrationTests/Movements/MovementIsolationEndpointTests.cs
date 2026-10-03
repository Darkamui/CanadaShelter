using System.Net;
using System.Net.Http.Json;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Movements;

/// <summary>
/// M3-5: the movement ledger never crosses a tenant boundary through the HTTP surface (an animal, location or person
/// of another organization is unknown to the caller), and voiding needs the administrator-only <c>movement.amend</c>
/// permission.
/// </summary>
public sealed class MovementIsolationEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string MovementsPath = "/api/movements";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organizationA = Guid.CreateVersion7();
    private readonly Guid _organizationB = Guid.CreateVersion7();

    [Fact]
    public async Task Void_movement_from_other_tenant_returns_404_and_leaves_the_ledger_unchanged()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var adminB = await ClientAsync(_organizationB, SystemRoles.Administrator);
        var (animalId, _) = await TakeInAsync(staffA);
        var intake = (await ListAsync(staffA, animalId)).Single();

        using var response = await adminB.PostAsJsonAsync(
            new Uri($"{MovementsPath}/{intake.Id}/void", UriKind.Relative), new { reason = "Erreur" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var after = await ListAsync(staffA, animalId);
        Assert.Equal(intake.Id, Assert.Single(after).Id);
        Assert.Null(after[0].VoidedByMovementId);
    }

    [Fact]
    public async Task List_movements_of_other_tenants_animal_returns_an_empty_list()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var staffB = await ClientAsync(_organizationB, "staff");
        var (animalId, _) = await TakeInAsync(staffA);

        var own = await ListAsync(staffA, animalId);
        var foreign = await ListAsync(staffB, animalId);

        Assert.Single(own);
        Assert.Empty(foreign);
    }

    [Fact]
    public async Task Record_intake_for_other_tenants_animal_is_rejected_on_animalId_and_records_nothing()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var staffB = await ClientAsync(_organizationB, "staff");
        var animalInA = await CreateAnimalAsync(staffA);
        var locationInB = await CreateLocationAsync(staffB);

        var problem = await PostInvalidAsync(
            staffB, "intakes", new { animalId = animalInA, reasonCode = "stray", toLocationId = locationInB });

        Assert.Contains("animalId", problem.Errors.Keys);
        Assert.Empty(await ListAsync(staffA, animalInA));
        Assert.Empty(await ListAsync(staffB, animalInA));
    }

    [Fact]
    public async Task Record_intake_into_other_tenants_location_is_rejected_on_toLocationId_and_records_nothing()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var staffB = await ClientAsync(_organizationB, "staff");
        var locationInA = await CreateLocationAsync(staffA);
        var animalInB = await CreateAnimalAsync(staffB);

        var problem = await PostInvalidAsync(
            staffB, "intakes", new { animalId = animalInB, reasonCode = "stray", toLocationId = locationInA });

        Assert.Contains("toLocationId", problem.Errors.Keys);
        Assert.Empty(await ListAsync(staffB, animalInB));
    }

    [Fact]
    public async Task Record_intake_with_other_tenants_person_is_rejected_on_personId_and_records_nothing()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var staffB = await ClientAsync(_organizationB, "staff");
        var personInA = await CreatePersonAsync(staffA);
        var animalInB = await CreateAnimalAsync(staffB);
        var locationInB = await CreateLocationAsync(staffB);

        var problem = await PostInvalidAsync(
            staffB, "intakes", new { animalId = animalInB, reasonCode = "stray", toLocationId = locationInB, personId = personInA });

        Assert.Contains("personId", problem.Errors.Keys);
        Assert.Empty(await ListAsync(staffB, animalInB));
    }

    [Fact]
    public async Task Record_adoption_by_other_tenants_person_is_rejected_on_personId_and_leaves_the_animal_in_care()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        using var staffB = await ClientAsync(_organizationB, "staff");
        var personInA = await CreatePersonAsync(staffA);
        var (animalInB, _) = await TakeInAsync(staffB);

        var problem = await PostInvalidAsync(
            staffB, "outcomes", new { animalId = animalInB, outcomeCode = "adoption", personId = personInA });

        Assert.Contains("personId", problem.Errors.Keys);
        Assert.Equal("intake", Assert.Single(await ListAsync(staffB, animalInB)).Type);
    }

    [Fact]
    public async Task Void_movement_as_staff_returns_403_and_leaves_the_ledger_unchanged()
    {
        using var staffA = await ClientAsync(_organizationA, "staff");
        var (animalId, _) = await TakeInAsync(staffA);
        var intake = (await ListAsync(staffA, animalId)).Single();

        using var response = await staffA.PostAsJsonAsync(
            new Uri($"{MovementsPath}/{intake.Id}/void", UriKind.Relative), new { reason = "Erreur" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(await ListAsync(staffA, animalId));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> ClientAsync(Guid organizationId, string role)
    {
        var user = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organizationId, role);
        return await _factory.CreateAntiforgeryClientAsync(user, organizationId);
    }

    private static async Task<(Guid AnimalId, Guid LocationId)> TakeInAsync(HttpClient client)
    {
        var animalId = await CreateAnimalAsync(client);
        var locationId = await CreateLocationAsync(client);
        using var response = await client.PostAsJsonAsync(
            new Uri($"{MovementsPath}/intakes", UriKind.Relative),
            new { animalId, reasonCode = "stray", toLocationId = locationId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (animalId, locationId);
    }

    private static async Task<Guid> CreateAnimalAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/animals", UriKind.Relative), new { name = "Pistache", speciesCode = "dog" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdDto>(TestContext.Current.CancellationToken))!.Id;
    }

    private static async Task<Guid> CreateLocationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/operations/locations", UriKind.Relative), new { kindCode = "kennel", name = "Enclos 4" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdDto>(TestContext.Current.CancellationToken))!.Id;
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/people", UriKind.Relative), new { displayName = "Marie Tremblay" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedPersonDto>(TestContext.Current.CancellationToken))!.Person.Id;
    }

    private static async Task<List<MovementDto>> ListAsync(HttpClient client, Guid animalId) =>
        (await client.GetFromJsonAsync<List<MovementDto>>(
            new Uri($"{MovementsPath}?animalId={animalId}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<ValidationProblemDto> PostInvalidAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri($"{MovementsPath}/{path}", UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;
    }

    private sealed record IdDto(Guid Id);

    private sealed record CreatedPersonDto(IdDto Person);

    private sealed record MovementDto(Guid Id, string Type, Guid? VoidedByMovementId);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);
}
