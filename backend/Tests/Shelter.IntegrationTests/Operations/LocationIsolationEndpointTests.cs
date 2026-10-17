using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Operations;

/// <summary>
/// M3-3: locations never cross a tenant boundary through the HTTP surface, the caller's tenant cannot be overridden
/// from the request body, a tenant's kind overrides never affect another tenant, and role-scoped permissions are
/// enforced on every endpoint.
/// </summary>
public sealed class LocationIsolationEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string LocationsPath = "/api/operations/locations";
    private const string KindsPath = "/api/operations/location-kinds";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organizationA = Guid.CreateVersion7();
    private readonly Guid _organizationB = Guid.CreateVersion7();

    [Fact]
    public async Task List_from_other_tenant_never_shows_the_location_with_or_without_archived()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { kindCode = "building", name = "Pavillon A" });

        var activeOnly = await ListAsync(clientB, string.Empty);
        var withArchived = await ListAsync(clientB, "includeArchived=true");

        Assert.DoesNotContain(created.Id, activeOnly.Select(l => l.Id));
        Assert.DoesNotContain(created.Id, withArchived.Select(l => l.Id));
    }

    [Fact]
    public async Task Update_location_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { kindCode = "building", name = "Original" });

        using var update = await clientB.PutAsJsonAsync(
            new Uri($"{LocationsPath}/{created.Id}", UriKind.Relative),
            new { kindCode = "building", name = "Modifié" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var stillOriginal = (await ListAsync(clientA, string.Empty)).Single(l => l.Id == created.Id);
        Assert.Equal("Original", stillOriginal.Name);
    }

    [Fact]
    public async Task Archive_location_from_other_tenant_returns_404_and_leaves_the_row_unchanged()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var created = await CreateAsync(clientA, new { kindCode = "building", name = "Pavillon" });

        Assert.Equal(HttpStatusCode.NotFound, await ArchiveAsync(clientB, created.Id));

        var stillActive = (await ListAsync(clientA, string.Empty)).Single(l => l.Id == created.Id);
        Assert.False(stillActive.IsArchived);
    }

    [Fact]
    public async Task Create_under_another_tenants_location_is_rejected_and_nothing_is_created()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var parentInA = await CreateAsync(clientA, new { kindCode = "building", name = "Pavillon A" });

        var problem = await PostInvalidAsync(clientB, new { parentId = parentInA.Id, kindCode = "room", name = "Salle" });

        Assert.Contains("parentId", problem.Errors.Keys);
        var tree = await ListAsync(clientA, string.Empty);
        Assert.DoesNotContain(tree, l => l.ParentId == parentInA.Id);
    }

    [Fact]
    public async Task Move_own_location_under_another_tenants_location_is_rejected()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        var parentInA = await CreateAsync(clientA, new { kindCode = "building", name = "Pavillon A" });
        var ownInB = await CreateAsync(clientB, new { kindCode = "building", name = "Pavillon B" });

        using var move = await clientB.PutAsJsonAsync(
            new Uri($"{LocationsPath}/{ownInB.Id}", UriKind.Relative),
            new { parentId = parentInA.Id, kindCode = "building", name = "Pavillon B" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        var problem = (await move.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;
        Assert.Contains("parentId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_ignores_a_tenant_id_from_the_request_body()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);

        var created = await CreateAsync(clientA, new
        {
            kindCode = "building",
            name = "Corps étranger",
            tenantId = _organizationB,
            organizationId = _organizationB,
        });

        var seenByA = (await ListAsync(clientA, string.Empty)).SingleOrDefault(l => l.Id == created.Id);
        Assert.NotNull(seenByA);

        var seenByB = (await ListAsync(clientB, string.Empty)).SingleOrDefault(l => l.Id == created.Id);
        Assert.Null(seenByB);
    }

    [Fact]
    public async Task A_kind_hidden_by_one_tenant_stays_visible_and_usable_for_another()
    {
        using var clientA = await StaffClientAsync(_organizationA);
        using var clientB = await StaffClientAsync(_organizationB);
        await HideKindAsync(_organizationA, "cage");

        var kindsB = await ListKindsAsync(clientB);
        Assert.Contains(kindsB, k => k.Code == "cage");

        var created = await CreateAsync(clientB, new { kindCode = "cage", name = "Cage 1" });
        Assert.Equal("cage", created.KindCode);
    }

    [Fact]
    public async Task Read_only_may_read_locations_but_not_write_them()
    {
        using var staff = await StaffClientAsync(_organizationA);
        var created = await CreateAsync(staff, new { kindCode = "building", name = "Lecture seule" });

        var readOnlyUser = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organizationA, SystemRoles.ReadOnly);
        using var readOnly = await _factory.CreateAntiforgeryClientAsync(readOnlyUser, _organizationA);

        using var kinds = await readOnly.GetAsync(new Uri(KindsPath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, kinds.StatusCode);

        using var list = await readOnly.GetAsync(new Uri(LocationsPath, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var create = await readOnly.PostAsJsonAsync(
            new Uri(LocationsPath, UriKind.Relative), new { kindCode = "building", name = "Jamais" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var update = await readOnly.PutAsJsonAsync(
            new Uri($"{LocationsPath}/{created.Id}", UriKind.Relative),
            new { kindCode = "building", name = "Jamais" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, await ArchiveAsync(readOnly, created.Id));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> StaffClientAsync(Guid organizationId)
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organizationId, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, organizationId);
    }

    // The override table has no endpoint yet (ADR 0017): hide a system kind for this organization directly.
    private async Task HideKindAsync(Guid organizationId, string code)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(organizationId);
        var overrideId = Guid.CreateVersion7();
        await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            _ => scope.ServiceProvider.GetRequiredService<ShelterDbContext>().Database.ExecuteSqlAsync(
                $"""
                INSERT INTO operations.location_kind_override (id, tenant_id, code, label_fr, label_en, sort_order, is_hidden, holds_animals)
                SELECT {overrideId}, {organizationId}, code, label_fr, label_en, sort_order, TRUE, holds_animals
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
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;
    }

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
}
