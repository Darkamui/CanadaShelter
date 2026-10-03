using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Operations;

/// <summary>
/// M3-3: staff build and reorganize the location tree. Names are unique among active siblings, a move never forms a
/// cycle, new kinds come from the organization's visible list, and a location with active children cannot be archived.
/// </summary>
public sealed class LocationEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string LocationsPath = "/api/operations/locations";
    private const string KindsPath = "/api/operations/location-kinds";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organization = Guid.CreateVersion7();

    [Fact]
    public async Task Kinds_are_listed_in_order_with_both_labels()
    {
        using var client = await StaffClientAsync();

        var kinds = await ListKindsAsync(client);

        Assert.Equal("shelter", kinds[0].Code);
        Assert.Equal("Refuge", kinds[0].Label.Fr);
        Assert.Equal("Shelter", kinds[0].Label.En);
        Assert.False(kinds.Single(k => k.Code == "building").HoldsAnimals);
        Assert.True(kinds.Single(k => k.Code == "kennel").HoldsAnimals);
    }

    [Fact]
    public async Task Create_nested_locations_then_list_the_tree()
    {
        using var client = await StaffClientAsync();
        var shelter = await CreateAsync(client, new { kindCode = "shelter", name = " Refuge principal " });
        var room = await CreateAsync(client, new { parentId = shelter.Id, kindCode = "room", name = "Salle des chats", capacity = 12 });

        var tree = await ListAsync(client, string.Empty);

        Assert.Equal("Refuge principal", shelter.Name);
        Assert.Null(shelter.ParentId);
        Assert.Equal(new[] { shelter.Id, room.Id }.Order(), tree.Select(l => l.Id).Order());
        var listedRoom = tree.Single(l => l.Id == room.Id);
        Assert.Equal(shelter.Id, listedRoom.ParentId);
        Assert.Equal(12, listedRoom.Capacity);
        Assert.Equal("room", listedRoom.KindCode);
    }

    [Fact]
    public async Task Sibling_names_are_unique_ignoring_accents_and_case_but_reusable_elsewhere_or_after_archive()
    {
        using var client = await StaffClientAsync();
        var a = await CreateAsync(client, new { kindCode = "building", name = "Pavillon A" });
        var b = await CreateAsync(client, new { kindCode = "building", name = "Pavillon B" });
        var salle = await CreateAsync(client, new { parentId = a.Id, kindCode = "room", name = "Salle Éclair" });

        Assert.Contains("name", (await PostInvalidAsync(client, new { parentId = a.Id, kindCode = "room", name = "SALLE ECLAIR" })).Errors.Keys);
        Assert.Contains("name", (await PostInvalidAsync(client, new { kindCode = "building", name = "pavillon a" })).Errors.Keys);

        await CreateAsync(client, new { parentId = b.Id, kindCode = "room", name = "Salle Éclair" });
        Assert.Equal(HttpStatusCode.NoContent, await ArchiveAsync(client, salle.Id));
        await CreateAsync(client, new { parentId = a.Id, kindCode = "room", name = "Salle Éclair" });
    }

    [Fact]
    public async Task Update_renames_and_moves_but_never_inside_itself()
    {
        using var client = await StaffClientAsync();
        var a = await CreateAsync(client, new { kindCode = "building", name = "A" });
        var b = await CreateAsync(client, new { parentId = a.Id, kindCode = "room", name = "B" });
        var c = await CreateAsync(client, new { parentId = b.Id, kindCode = "kennel", name = "C" });

        using (var move = await PutAsync(client, c.Id, new { parentId = a.Id, kindCode = "cage", name = "C2", capacity = 2 }))
        {
            Assert.Equal(HttpStatusCode.OK, move.StatusCode);
            var moved = await move.Content.ReadFromJsonAsync<LocationDto>(TestContext.Current.CancellationToken);
            Assert.Equal(a.Id, moved!.ParentId);
            Assert.Equal("C2", moved.Name);
            Assert.Equal("cage", moved.KindCode);
        }

        using (var intoChild = await PutAsync(client, a.Id, new { parentId = b.Id, kindCode = "building", name = "A" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, intoChild.StatusCode);
            Assert.Contains("parentId", (await ReadProblemAsync(intoChild)).Errors.Keys);
        }

        using (var intoSelf = await PutAsync(client, a.Id, new { parentId = a.Id, kindCode = "building", name = "A" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, intoSelf.StatusCode);
            Assert.Contains("parentId", (await ReadProblemAsync(intoSelf)).Errors.Keys);
        }

        Assert.Null((await ListAsync(client, string.Empty)).Single(l => l.Id == a.Id).ParentId);
    }

    [Fact]
    public async Task Unknown_or_hidden_kinds_are_rejected_for_new_choices_only()
    {
        using var client = await StaffClientAsync();
        var cage = await CreateAsync(client, new { kindCode = "cage", name = "Cage 1" });
        await HideKindAsync("cage");

        Assert.Contains("kindCode", (await PostInvalidAsync(client, new { kindCode = "spaceship", name = "X" })).Errors.Keys);
        Assert.Contains("kindCode", (await PostInvalidAsync(client, new { kindCode = "cage", name = "Cage 2" })).Errors.Keys);
        Assert.DoesNotContain(await ListKindsAsync(client), k => k.Code == "cage");

        // The location that already has the hidden kind can still be renamed.
        using var rename = await PutAsync(client, cage.Id, new { kindCode = "cage", name = "Cage un" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task Parent_must_exist_and_be_active()
    {
        using var client = await StaffClientAsync();
        var archived = await CreateAsync(client, new { kindCode = "building", name = "Ancien" });
        Assert.Equal(HttpStatusCode.NoContent, await ArchiveAsync(client, archived.Id));

        Assert.Contains("parentId", (await PostInvalidAsync(client, new { parentId = Guid.NewGuid(), kindCode = "room", name = "X" })).Errors.Keys);
        Assert.Contains("parentId", (await PostInvalidAsync(client, new { parentId = archived.Id, kindCode = "room", name = "X" })).Errors.Keys);
    }

    [Fact]
    public async Task Request_validation_reports_each_field()
    {
        using var client = await StaffClientAsync();

        var problem = await PostInvalidAsync(client, new { name = " ", capacity = 10_001 });

        Assert.Contains("name", problem.Errors.Keys);
        Assert.Contains("kindCode", problem.Errors.Keys);
        Assert.Contains("capacity", problem.Errors.Keys);
    }

    [Fact]
    public async Task Archive_is_blocked_while_active_children_remain()
    {
        using var client = await StaffClientAsync();
        var building = await CreateAsync(client, new { kindCode = "building", name = "Pavillon" });
        var room = await CreateAsync(client, new { parentId = building.Id, kindCode = "room", name = "Salle" });

        using (var blocked = await client.PostAsync(
            new Uri($"{LocationsPath}/{building.Id}/archive", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var problem = await blocked.Content.ReadFromJsonAsync<ConflictDto>(TestContext.Current.CancellationToken);
            Assert.Equal("location.hasActiveChildren", problem!.Code);
        }

        Assert.Equal(HttpStatusCode.NoContent, await ArchiveAsync(client, room.Id));
        Assert.Equal(HttpStatusCode.NoContent, await ArchiveAsync(client, building.Id));
        Assert.Equal(HttpStatusCode.NoContent, await ArchiveAsync(client, building.Id));
        Assert.Equal(HttpStatusCode.NotFound, await ArchiveAsync(client, Guid.NewGuid()));

        Assert.Empty(await ListAsync(client, string.Empty));
        var all = await ListAsync(client, "includeArchived=true");
        Assert.Equal(2, all.Count);
        Assert.All(all, l => Assert.True(l.IsArchived));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> StaffClientAsync()
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, _organization);
    }

    // The override table has no endpoint yet (ADR 0017): hide a system kind for this organization directly.
    private async Task HideKindAsync(string code)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_organization);
        var overrideId = Guid.CreateVersion7();
        await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            _ => scope.ServiceProvider.GetRequiredService<ShelterDbContext>().Database.ExecuteSqlAsync(
                $"""
                INSERT INTO operations.location_kind_override (id, tenant_id, code, label_fr, label_en, sort_order, is_hidden, holds_animals)
                SELECT {overrideId}, {_organization}, code, label_fr, label_en, sort_order, TRUE, holds_animals
                FROM operations.location_kind WHERE code = {code}
                """,
                TestContext.Current.CancellationToken),
            static _ => true,
            TestContext.Current.CancellationToken);
    }

    private static async Task<List<KindDto>> ListKindsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<KindDto>>(new Uri(KindsPath, UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<LocationDto> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(LocationsPath, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<ValidationProblemDto> PostInvalidAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(LocationsPath, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return await ReadProblemAsync(response);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, object body) =>
        client.PutAsJsonAsync(new Uri($"{LocationsPath}/{id}", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private static async Task<ValidationProblemDto> ReadProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;

    private static async Task<List<LocationDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<List<LocationDto>>(new Uri($"{LocationsPath}?{query}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<HttpStatusCode> ArchiveAsync(HttpClient client, Guid id)
    {
        using var response = await client.PostAsync(
            new Uri($"{LocationsPath}/{id}/archive", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed record LabelDto(string Fr, string En);

    private sealed record KindDto(string Code, LabelDto Label, bool HoldsAnimals);

    private sealed record LocationDto(Guid Id, Guid? ParentId, string KindCode, string Name, int? Capacity, bool IsArchived);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);

    private sealed record ConflictDto(string Code);
}
