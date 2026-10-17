using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Identity;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.DevSeed;

/// <summary>
/// ADR 0023: <c>POST /api/platform/dev/seed</c> fills the caller's organization, or a new one, through each module's own
/// domain code. It exists only in Development with <c>DevSeed:Enabled</c>, only administrators may call it, and data
/// never lands in another organization.
/// </summary>
public sealed class DevSeedEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string SeedPath = "/api/platform/dev/seed";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString)
    {
        EnvironmentName = "Development",
        Settings = new Dictionary<string, string?>
        {
            ["DevSeed:Enabled"] = "true",
            ["ConnectionStrings:PlatformAdmin"] = postgres.Database.PlatformAdminConnectionString,
        },

        // The startup seed would create the shared demo organization in the test database.
        ConfigureServices = services => services.RemoveAll<IHostedService>(),
    };

    [Fact]
    public async Task Administrator_seeds_the_current_organization_with_consistent_custody()
    {
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, SystemRoles.Administrator);
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, organization);

        var counts = await SeedAsync(client, new { scale = "small", seed = 7 });

        Assert.Equal(25, counts["animals"]);
        Assert.Equal(20, counts["people"]);
        Assert.True(counts["locations"] > 10);
        Assert.True(counts["movements.intake"] > 20);
        Assert.True(counts["movements.outcome"] > 0);
        Assert.Equal(25L, await ScalarAsync(organization, "SELECT count(*) FROM animals.animal"));
        Assert.Equal(20L, await ScalarAsync(organization, "SELECT count(*) FROM people.person"));
        Assert.True(await ScalarAsync(organization, "SELECT count(*) FROM movements.movement") >= counts["movements.intake"]);

        // Audited like any change, with the caller as actor and the seed as source.
        Assert.True(await ScalarAsync(organization, "SELECT count(*) FROM audit.audit_event") > 0);
        Assert.Equal(0L, await ScalarAsync(
            organization,
            $"SELECT count(*) FROM audit.audit_event WHERE source <> 'dev-seed' OR actor_id IS DISTINCT FROM '{admin:D}'"));

        // Every animal in care is where its latest movement still in effect put it.
        Assert.Equal(0L, await ScalarAsync(
            organization,
            """
            SELECT count(*) FROM animals.animal a
            WHERE a.custody_status = 'in_care' AND a.current_location_id IS DISTINCT FROM (
                SELECT m.to_location_id FROM movements.movement m
                WHERE m.animal_id = a.id AND m.type <> 'void'
                  AND NOT EXISTS (SELECT 1 FROM movements.movement v WHERE v.voids_movement_id = m.id)
                ORDER BY m.id DESC LIMIT 1)
            """));
    }

    [Fact]
    public async Task Seeding_the_same_organization_twice_adds_data_and_reuses_the_location_tree()
    {
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, SystemRoles.Administrator);
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, organization);

        await SeedAsync(client, new { factor = 1, seed = 3 });
        var locations = await ScalarAsync(organization, "SELECT count(*) FROM operations.location");
        var second = await SeedAsync(client, new { factor = 1, seed = 3 });

        Assert.False(second.ContainsKey("locations"));
        Assert.Equal(locations, await ScalarAsync(organization, "SELECT count(*) FROM operations.location"));
        Assert.Equal(50L, await ScalarAsync(organization, "SELECT count(*) FROM animals.animal"));
    }

    [Fact]
    public async Task New_organization_mode_seeds_a_fresh_organization_and_leaves_the_current_one_alone()
    {
        var current = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, current, SystemRoles.Administrator);
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, current);

        var response = await client.PostAsJsonAsync(
            SeedPath, new { scale = "small", newOrganization = true, organizationName = "Refuge test frais" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var fresh = body.GetProperty("organizationId").GetGuid();

        Assert.NotEqual(current, fresh);
        Assert.Equal("Refuge test frais", body.GetProperty("organizationName").GetString());
        Assert.Equal(25L, await ScalarAsync(fresh, "SELECT count(*) FROM animals.animal"));
        Assert.Equal(
            1L,
            await ScalarAsync(fresh, $"SELECT count(*) FROM platform.staff_membership WHERE user_id = '{admin:D}' AND 'administrator' = ANY(role_keys)"));
        Assert.Equal(0L, await ScalarAsync(current, "SELECT count(*) FROM animals.animal"));
        Assert.Equal(0L, await ScalarAsync(current, "SELECT count(*) FROM people.person"));
    }

    [Theory]
    [InlineData(SystemRoles.Staff)]
    [InlineData(SystemRoles.ReadOnly)]
    public async Task Non_administrators_are_denied(string role)
    {
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var member = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, role);
        using var client = await _factory.CreateAntiforgeryClientAsync(member, organization);

        var response = await client.PostAsJsonAsync(SeedPath, new { scale = "small" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await ScalarAsync(organization, "SELECT count(*) FROM animals.animal"));
    }

    [Theory]
    [InlineData("""{ "factor": 0 }""", "factor")]
    [InlineData("""{ "factor": 201 }""", "factor")]
    [InlineData("""{ "scale": "huge" }""", "scale")]
    [InlineData("""{ "organizationName": "Sans nouvel organisme" }""", "organizationName")]
    public async Task Invalid_requests_are_rejected(string json, string field)
    {
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, SystemRoles.Administrator);
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, organization);

        var response = await client.PostAsync(
            SeedPath, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("Test", "true")]
    [InlineData("Development", "false")]
    public async Task Route_does_not_exist_unless_development_with_the_flag(string environment, string enabled)
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            EnvironmentName = environment,
            Settings = new Dictionary<string, string?> { ["DevSeed:Enabled"] = enabled },
            ConfigureServices = services => services.RemoveAll<IHostedService>(),
        };
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, SystemRoles.Administrator);
        using var client = await factory.CreateAntiforgeryClientAsync(admin, organization);

        var response = await client.PostAsJsonAsync(SeedPath, new { scale = "small" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<Dictionary<string, int>> SeedAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync(SeedPath, request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("counts").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
    }

    /// <summary>A count read as the runtime role inside <paramref name="organization"/> (RLS applies).</summary>
    private async Task<long> ScalarAsync(Guid organization, string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            set.Parameters.AddWithValue("tenant", organization.ToString("D"));
            await set.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
