using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.People;

/// <summary>
/// M3-2: staff create, find, edit and archive people. Search ignores accents and case and matches phone digits;
/// creating a likely duplicate warns without blocking; personal values stay out of the audit trail in clear.
/// </summary>
public sealed class PeopleEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string PeoplePath = "/api/people";
    private static readonly string[] FosterAdopter = ["foster", "adopter"];
    private static readonly string[] Volunteer = ["volunteer"];
    private static readonly string[] UnknownRole = ["boss"];

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organization = Guid.CreateVersion7();

    [Fact]
    public async Task Create_then_get_returns_the_normalized_person()
    {
        using var client = await StaffClientAsync();

        var created = await CreateAsync(client, new
        {
            firstName = " Élodie ",
            lastName = "Gagnon",
            email = "elodie@exemple.ca",
            phone = "514 555-0100",
            province = "qc",
            roleTags = FosterAdopter,
        });

        Assert.Empty(created.PossibleDuplicates);
        var person = await client.GetFromJsonAsync<PersonDto>(
            new Uri($"{PeoplePath}/{created.Person.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.NotNull(person);
        Assert.Equal("Élodie Gagnon", person.DisplayName);
        Assert.Equal("Élodie", person.FirstName);
        Assert.Equal("QC", person.Province);
        Assert.Equal("fr", person.PreferredLanguage);
        Assert.Equal(["adopter", "foster"], person.RoleTags);
        Assert.False(person.IsArchived);
    }

    [Fact]
    public async Task Search_ignores_accents_and_case_and_matches_phone_digits()
    {
        using var client = await StaffClientAsync();
        var eclair = await CreateAsync(client, new { firstName = "Éclair", lastName = "Côté", phone = "(514) 555-0142" });
        var other = await CreateAsync(client, new { firstName = "Marc", lastName = "Tremblay", email = "marc@exemple.ca", phone = "450-555-0199" });

        Assert.Equal([eclair.Person.Id], await SearchIdsAsync(client, "q=ECLAIR"));
        Assert.Equal([eclair.Person.Id], await SearchIdsAsync(client, "q=cote"));
        Assert.Equal([eclair.Person.Id], await SearchIdsAsync(client, "q=555-0142"));
        Assert.Equal([other.Person.Id], await SearchIdsAsync(client, "q=marc%40exemple"));
        Assert.Empty(await SearchIdsAsync(client, "q=nobody"));
    }

    // Wildcards are punctuation: normalized away, so they never reach LIKE; a query with nothing left filters nothing.
    [Fact]
    public async Task Search_of_punctuation_only_filters_nothing()
    {
        using var client = await StaffClientAsync();
        var alice = await CreateAsync(client, new { firstName = "Alice" });

        Assert.Equal([alice.Person.Id], await SearchIdsAsync(client, "q=%25"));
        Assert.Equal([alice.Person.Id], await SearchIdsAsync(client, "q=_"));
        Assert.Empty(await SearchIdsAsync(client, "q=a_i"));
    }

    [Fact]
    public async Task List_filters_by_role_and_archived_and_pages_in_name_order()
    {
        using var client = await StaffClientAsync();
        var carole = await CreateAsync(client, new { displayName = "Carole", roleTags = Volunteer });
        var alain = await CreateAsync(client, new { displayName = "Alain" });
        var berthe = await CreateAsync(client, new { displayName = "Berthe", roleTags = Volunteer });
        var archived = await CreateAsync(client, new { displayName = "Archivé" });
        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, archived.Person.Id, "archive"));

        var firstPage = await ListAsync(client, "pageSize=2");
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal([alain.Person.Id, berthe.Person.Id], firstPage.Items.Select(p => p.Id));
        Assert.Equal([carole.Person.Id], (await ListAsync(client, "pageSize=2&page=2")).Items.Select(p => p.Id));
        Assert.Equal([carole.Person.Id, berthe.Person.Id], (await ListAsync(client, "sort=-name&role=volunteer")).Items.Select(p => p.Id));
        Assert.Equal([archived.Person.Id], (await ListAsync(client, "archived=true")).Items.Select(p => p.Id));

        using var badRole = await client.GetAsync(new Uri($"{PeoplePath}?role=boss", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
    }

    [Fact]
    public async Task Create_warns_about_people_with_the_same_email_or_phone()
    {
        using var client = await StaffClientAsync();
        var byEmail = await CreateAsync(client, new { displayName = "Premier", email = "Dup@Exemple.ca" });
        var byPhone = await CreateAsync(client, new { displayName = "Second", secondaryPhone = "+1 514 555 0177" });

        var created = await CreateAsync(client, new { displayName = "Nouveau", email = "dup@exemple.ca", phone = "514-555-0177" });

        Assert.Equal(new[] { byEmail.Person.Id, byPhone.Person.Id }.Order(), created.PossibleDuplicates.Select(d => d.Id).Order());
        Assert.Equal(3, (await ListAsync(client, string.Empty)).TotalCount);
    }

    [Fact]
    public async Task Update_replaces_fields_and_archive_round_trips()
    {
        using var client = await StaffClientAsync();
        var created = await CreateAsync(client, new { firstName = "Jean", email = "jean@exemple.ca" });
        var id = created.Person.Id;

        using var update = await client.PutAsJsonAsync(
            new Uri($"{PeoplePath}/{id}", UriKind.Relative),
            new { firstName = "Jeanne", preferredLanguage = "en" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PersonDto>(TestContext.Current.CancellationToken);
        Assert.Equal("Jeanne", updated!.DisplayName);
        Assert.Null(updated.Email);
        Assert.Equal("en", updated.PreferredLanguage);

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, id, "archive"));
        Assert.Empty((await ListAsync(client, string.Empty)).Items);
        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(client, id, "unarchive"));
        Assert.Single((await ListAsync(client, string.Empty)).Items);
    }

    [Fact]
    public async Task Invalid_person_is_rejected_with_field_errors()
    {
        using var client = await StaffClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri(PeoplePath, UriKind.Relative),
            new { email = "pas-un-courriel", province = "ZZ", roleTags = UnknownRole },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken);
        Assert.Equal(["displayName", "email", "province", "roleTags"], problem!.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Audit_trail_holds_no_personal_value_in_clear()
    {
        using var client = await StaffClientAsync();
        var created = await CreateAsync(client, new { firstName = "Secrète", email = "secret@exemple.ca", phone = "514 555 0123" });

        var audit = Assert.Single(await ReadAuditAsync(created.Person.Id));
        Assert.DoesNotContain("Secrète", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("secret@exemple.ca", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("5145550123", audit, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> StaffClientAsync()
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, _organization);
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

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, Guid personId, string action)
    {
        using var response = await client.PostAsync(
            new Uri($"{PeoplePath}/{personId}/{action}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private async Task<List<string>> ReadAuditAsync(Guid personId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_organization);
        return await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            _ => scope.ServiceProvider.GetRequiredService<ShelterDbContext>().Database
                .SqlQuery<string>(
                    $"""
                    SELECT coalesce(before_json::text, '') || coalesce(after_json::text, '') AS "Value"
                    FROM audit.audit_event
                    WHERE entity_id = {personId.ToString("D")}
                    """)
                .ToListAsync(TestContext.Current.CancellationToken),
            static _ => true,
            TestContext.Current.CancellationToken);
    }

    private sealed record PersonDto(
        Guid Id, string? FirstName, string DisplayName, string? Email, string? Province, string PreferredLanguage, string[] RoleTags, bool IsArchived);

    private sealed record MatchDto(Guid Id, string DisplayName, bool IsArchived);

    private sealed record CreatedDto(PersonDto Person, List<MatchDto> PossibleDuplicates);

    private sealed record ListItemDto(Guid Id, string DisplayName);

    private sealed record PageDto(List<ListItemDto> Items, int Page, int PageSize, int TotalCount);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);
}
