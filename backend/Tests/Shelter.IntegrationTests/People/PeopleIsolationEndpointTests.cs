using System.Net;
using System.Net.Http.Json;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.People;

/// <summary>
/// M3-2: people never cross a tenant boundary through the HTTP surface, the caller's tenant cannot be overridden
/// from the request body, and role-scoped permissions are enforced on every endpoint.
/// </summary>
public sealed class PeopleIsolationEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string PeoplePath = "/api/people";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organizationA = Guid.CreateVersion7();
    private readonly Guid _organizationB = Guid.CreateVersion7();

    [Fact]
    public async Task Get_person_from_other_tenant_returns_404()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { firstName = "Alice", lastName = "Tenantaire" });

        using var response = await clientB.GetAsync(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_person_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { firstName = "Bruno", lastName = "Original" });

        using var update = await clientB.PutAsJsonAsync(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative),
            new { firstName = "Modifié" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var stillOriginal = await clientA.GetFromJsonAsync<PersonDto>(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal("Bruno Original", stillOriginal!.DisplayName);
    }

    [Fact]
    public async Task Archive_person_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { firstName = "Carla", lastName = "Archive" });

        Assert.Equal(HttpStatusCode.NotFound, await PostActionAsync(clientB, created.Person.Id, "archive"));

        var stillActive = await clientA.GetFromJsonAsync<PersonDto>(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.False(stillActive!.IsArchived);
    }

    [Fact]
    public async Task Unarchive_person_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { firstName = "Denis", lastName = "Unarchive" });
        Assert.Equal(HttpStatusCode.NoContent, await PostActionAsync(clientA, created.Person.Id, "archive"));

        Assert.Equal(HttpStatusCode.NotFound, await PostActionAsync(clientB, created.Person.Id, "unarchive"));

        var stillArchived = await clientA.GetFromJsonAsync<PersonDto>(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.True(stillArchived!.IsArchived);
    }

    [Fact]
    public async Task List_from_other_tenant_never_shows_the_person()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { firstName = "Emma", lastName = "Liste" });

        var page = await ListAsync(clientB, string.Empty);

        Assert.DoesNotContain(created.Person.Id, page.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Search_from_other_tenant_never_matches_the_person_by_name_email_or_phone()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new
        {
            firstName = "Fabienne",
            lastName = "Contact",
            email = "fabienne@exemple.ca",
            phone = "514 555-0188",
        });

        Assert.Empty(await SearchIdsAsync(clientB, "q=Fabienne"));
        Assert.Empty(await SearchIdsAsync(clientB, "q=fabienne%40exemple.ca"));
        Assert.Empty(await SearchIdsAsync(clientB, "q=555-0188"));
    }

    [Fact]
    public async Task Create_in_other_tenant_never_lists_the_matching_person_as_a_possible_duplicate()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        await CreateAsync(clientA, new { firstName = "Gilles", lastName = "Doublon", email = "gilles@exemple.ca", phone = "514 555-0199" });

        var created = await CreateAsync(clientB, new { firstName = "Gilles", lastName = "Doublon", email = "gilles@exemple.ca", phone = "514-555-0199" });

        Assert.Empty(created.PossibleDuplicates);
    }

    [Fact]
    public async Task Create_ignores_a_tenant_id_from_the_request_body()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);

        var created = await CreateAsync(clientA, new { firstName = "Hugo", lastName = "Corps", tenantId = _organizationB });

        var seenByA = await clientA.GetFromJsonAsync<PersonDto>(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.NotNull(seenByA);

        using var seenByB = await clientB.GetAsync(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, seenByB.StatusCode);
    }

    [Fact]
    public async Task Read_only_may_read_people_but_not_write_them()
    {
        using var staff = await StaffClientAsync(_organizationA);
        var created = await CreateAsync(staff, new { firstName = "Irène", lastName = "Lecture" });

        var readOnlyUser = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organizationA, SystemRoles.ReadOnly);
        using var readOnly = await _factory.CreateAntiforgeryClientAsync(readOnlyUser, _organizationA);

        using var list = await readOnly.GetAsync(new Uri(PeoplePath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var detail = await readOnly.GetAsync(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        using var create = await readOnly.PostAsJsonAsync(
            new Uri(PeoplePath, UriKind.Relative), new { firstName = "Jamais" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var update = await readOnly.PutAsJsonAsync(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), new { firstName = "Jamais" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, await PostActionAsync(readOnly, created.Person.Id, "archive"));
        Assert.Equal(HttpStatusCode.Forbidden, await PostActionAsync(readOnly, created.Person.Id, "unarchive"));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> StaffClientAsync(Guid organizationId)
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organizationId, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, organizationId);
    }

    private static async Task<CreatedDto> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(PeoplePath, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<PageDto> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PageDto>(new Uri($"{PeoplePath}?{query}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<List<Guid>> SearchIdsAsync(HttpClient client, string query) =>
        [.. (await ListAsync(client, query)).Items.Select(p => p.Id)];

    private static async Task<HttpStatusCode> PostActionAsync(HttpClient client, Guid personId, string action)
    {
        using var response = await client.PostAsync(
            new Uri($"{PeoplePath}/{personId}/{action}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed record PersonDto(Guid Id, string DisplayName, bool IsArchived);

    private sealed record MatchDto(Guid Id, string DisplayName, bool IsArchived);

    private sealed record CreatedDto(PersonDto Person, List<MatchDto> PossibleDuplicates);

    private sealed record ListItemDto(Guid Id);

    private sealed record PageDto(List<ListItemDto> Items, int Page, int PageSize, int TotalCount);
}
