using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>
/// M2-2: the tenant is the session's active organization, re-checked against an active membership on every
/// request; organization selection only chooses among the caller's own memberships; memberships are self-readable
/// before an organization is chosen, and never another account's.
/// </summary>
public sealed class MembershipTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string TenantPath = "/api/test-membership/tenant";
    private const string MembershipsPath = "/api/test-membership/memberships";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString, new MembershipProbeModule());

    [Fact]
    public async Task Active_member_gets_its_organization_as_tenant()
    {
        var organization = Guid.CreateVersion7();
        var user = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization);
        using var client = _factory.CreateHttpsClient(user, organization);

        using var response = await client.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(organization, await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Member_of_a_claiming_b_reads_no_tenant_data()
    {
        var organizationA = Guid.CreateVersion7();
        var organizationB = Guid.CreateVersion7();
        var user = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organizationA);
        using var client = _factory.CreateHttpsClient(user, organizationB);

        using var response = await client.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Suspended_membership_is_forbidden()
    {
        var organization = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, user, status: "Suspended");
        using var client = _factory.CreateHttpsClient(user, organization);

        using var response = await client.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Signed_in_account_without_membership_is_forbidden()
    {
        using var client = _factory.CreateHttpsClient(Guid.CreateVersion7(), Guid.CreateVersion7());

        using var response = await client.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_tenant_request_is_unauthorized()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Self_read_shows_only_the_callers_rows()
    {
        var organizationA = Guid.CreateVersion7();
        var organizationB = Guid.CreateVersion7();
        var caller = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationA, caller);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationB, caller, status: "Suspended");
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationA, other);

        // No active organization: only the self-read policy applies.
        using var client = _factory.CreateHttpsClient(caller);
        using var response = await client.GetAsync(new Uri(MembershipsPath, UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<MembershipRow>>(TestContext.Current.CancellationToken);
        Assert.NotNull(rows);
        Assert.All(rows, r => Assert.Equal(caller, r.UserId));
        Assert.Equal(new[] { organizationA, organizationB }.Order(), rows.Select(r => r.TenantId).Order());
    }

    [Fact]
    public async Task Self_read_needs_the_user_setting_and_never_allows_writes()
    {
        var organization = Guid.CreateVersion7();
        var user = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization);
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0L, await ScalarAsync(connection, "SELECT count(*) FROM platform.staff_membership"));

        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await ScalarAsync(connection, $"SELECT set_config('app.user_id', '{user:D}', true)", transaction);
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT count(*) FROM platform.staff_membership", transaction));

        // Self-read is SELECT only: a membership can only be written inside its organization.
        var error = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync(
            connection,
            $"INSERT INTO platform.staff_membership (id, tenant_id, user_id, status, created_at) VALUES (gen_random_uuid(), '{Guid.CreateVersion7():D}', '{user:D}', 'Active', now())",
            transaction));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Login_with_one_membership_selects_it()
    {
        var organization = await CreateOrganizationAsync("Refuge Un");
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, userId);
        using var client = new SessionClient(_factory);

        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();

        var session = await SessionAsync(client);
        Assert.Equal(organization, session.GetProperty("activeOrganizationId").GetGuid());
        var membership = Assert.Single(session.GetProperty("memberships").EnumerateArray());
        Assert.Equal(organization, membership.GetProperty("organizationId").GetGuid());
        Assert.Equal("Refuge Un", membership.GetProperty("organizationName").GetString());
        Assert.Equal(organization, await TenantAsync(client));
    }

    [Fact]
    public async Task Login_with_several_memberships_selects_none_until_chosen()
    {
        var organizationA = await CreateOrganizationAsync("Refuge A");
        var organizationB = await CreateOrganizationAsync("Refuge B");
        var foreign = await CreateOrganizationAsync("Refuge C");
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationA, userId);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationB, userId);
        using var client = new SessionClient(_factory);

        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();

        var session = await SessionAsync(client);
        Assert.Equal(JsonValueKind.Null, session.GetProperty("activeOrganizationId").ValueKind);
        Assert.Equal(
            [organizationA, organizationB],
            session.GetProperty("memberships").EnumerateArray().Select(m => m.GetProperty("organizationId").GetGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Http.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/platform/session/organization/{foreign:D}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/platform/session/organization/{organizationB:D}")).StatusCode);

        Assert.Equal(organizationB, (await SessionAsync(client)).GetProperty("activeOrganizationId").GetGuid());
        Assert.Equal(organizationB, await TenantAsync(client));
    }

    [Fact]
    public async Task Suspension_ends_tenant_access_of_an_existing_session()
    {
        var organization = await CreateOrganizationAsync("Refuge Suspendu");
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, userId);
        using var client = new SessionClient(_factory);
        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();
        Assert.Equal(organization, await TenantAsync(client));

        await SuspendAsync(organization, userId);

        // Same cookie, same claim: the membership is re-checked, not the claim trusted.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Http.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/platform/session/organization/{organization:D}")).StatusCode);
        Assert.Empty((await SessionAsync(client)).GetProperty("memberships").EnumerateArray());
    }

    [Fact]
    public async Task Account_without_membership_gets_no_staff_access()
    {
        // Staff access comes from a membership of the account's ID, never from its email address.
        var organization = await CreateOrganizationAsync("Refuge Sans Membre");
        var (_, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        using var client = new SessionClient(_factory);

        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();

        var session = await SessionAsync(client);
        Assert.Empty(session.GetProperty("memberships").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("activeOrganizationId").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Http.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/platform/session/organization/{organization:D}")).StatusCode);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<JsonElement> SessionAsync(SessionClient client)
    {
        using var response = await client.GetSessionAsync();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> TenantAsync(SessionClient client)
    {
        using var response = await client.Http.GetAsync(new Uri(TenantPath, UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    // Organizations are provisioned by the platform admin role; the runtime role only reads them.
    private async Task<Guid> CreateOrganizationAsync(string name)
    {
        var id = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(postgres.Database.PlatformAdminConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "INSERT INTO platform.organization (id, name, slug, status, created_at) VALUES (@id, @name, @slug, 'Active', now())",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("slug", $"org-{id:N}");
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return id;
    }

    // The runtime role has no UPDATE on memberships yet (staff management comes with M2-3). RLS is forced, so even
    // the table owner writes inside the organization.
    private async Task SuspendAsync(Guid organizationId, Guid userId)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            scope.Parameters.AddWithValue("tenant", organizationId.ToString("D"));
            await scope.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var command = new NpgsqlCommand(
            "UPDATE platform.staff_membership SET status = 'Suspended' WHERE user_id = @user",
            connection,
            transaction);
        command.Parameters.AddWithValue("user", userId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private sealed record MembershipRow(Guid TenantId, Guid UserId);

    /// <summary>
    /// Test-only module: an endpoint that declares nothing (so only the fallback policy guards it: default deny,
    /// member of the active organization required), and raw membership reads scoped only by the database.
    /// </summary>
    private sealed class MembershipProbeModule : IModule
    {
        public string RoutePrefix => "test-membership";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/tenant", (ITenantContext tenant) => Results.Json(tenant.TenantId));

            endpoints.MapGet("/memberships", async (ShelterDbContext db, CancellationToken ct) =>
                Results.Json(await db.Database
                    .SqlQuery<MembershipRow>($"SELECT tenant_id, user_id FROM platform.staff_membership")
                    .ToListAsync(ct)))
                .RequireSession();
        }
    }
}
