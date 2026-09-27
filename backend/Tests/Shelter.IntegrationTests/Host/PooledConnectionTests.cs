using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Host;

/// <summary>
/// M1-3, M2-2: with a pool of one, tenant A's request and tenant B's request share one physical connection, and
/// nothing of A (rows, the tenant setting or the user setting) is visible to B or to a later anonymous request.
/// </summary>
public sealed class PooledConnectionTests(PostgresFixture postgres)
{

    [Fact]
    public async Task Tenant_b_on_tenant_as_pooled_connection_sees_none_of_as_rows()
    {
        var singleConnection = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString)
        {
            MaxPoolSize = 1,
            ApplicationName = "pooled-connection-test", // own pool, not shared with other tests
        }.ConnectionString;
        await using var factory = new ShelterApiFactory(singleConnection, new PoolProbeModule());
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var userA = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, tenantA);
        var userB = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, tenantB);
        using var clientA = await factory.CreateAntiforgeryClientAsync(userA, tenantA);
        using var clientB = factory.CreateHttpsClient(userB, tenantB);
        using var anonymous = factory.CreateHttpsClient();

        var written = await SendAsync<Probe>(clientA, HttpMethod.Post);
        var readByB = await SendAsync<Probe>(clientB, HttpMethod.Get);
        var readWithoutTenant = await SendAsync<Probe>(anonymous, HttpMethod.Get);

        // Precondition: all three requests really shared one physical connection.
        Assert.True(written.Pid == readByB.Pid && written.Pid == readWithoutTenant.Pid, "setup: the pool did not reuse one connection");

        Assert.Equal(1, written.Count);
        Assert.Equal(0, readByB.Count);
        Assert.Equal(userA.ToString("D"), written.UserId);
        Assert.Equal(tenantB.ToString("D"), readByB.Tenant);
        Assert.Equal(userB.ToString("D"), readByB.UserId);
        Assert.Equal(0, readWithoutTenant.Count);
        Assert.True(string.IsNullOrEmpty(readWithoutTenant.Tenant), "the tenant setting outlived its transaction");
        Assert.True(string.IsNullOrEmpty(readWithoutTenant.UserId), "the user setting outlived its transaction");
    }

    private static async Task<T> SendAsync<T>(HttpClient client, HttpMethod method)
    {
        using var request = new HttpRequestMessage(method, new Uri("/api/test-pool/settings", UriKind.Relative));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private sealed record Probe(int Pid, int Count, string? Tenant, string? UserId);

    /// <summary>Test-only module: raw SQL, so only the database scopes what each request sees.</summary>
    private sealed class PoolProbeModule : IModule
    {
        private const string Key = "pooled-connection";

        public string RoutePrefix => "test-pool";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/settings", async (ShelterDbContext db, CancellationToken ct) =>
            {
                await db.Database.ExecuteSqlAsync(
                    $"INSERT INTO platform.tenant_setting (id, tenant_id, key, value) VALUES (gen_random_uuid(), platform.current_tenant_id(), {Key}, 'x')",
                    ct);
                return Results.Json(await ProbeAsync(db, ct));
            });

            endpoints.MapGet("/settings", async (ShelterDbContext db, CancellationToken ct) => Results.Json(await ProbeAsync(db, ct)));
        }

        private static Task<Probe> ProbeAsync(ShelterDbContext db, CancellationToken ct) =>
            db.Database
                .SqlQuery<Probe>(
                    $"""
                    SELECT pg_backend_pid() AS pid,
                           (SELECT count(*)::int FROM platform.tenant_setting WHERE key = {Key}) AS count,
                           current_setting('app.tenant_id', true) AS tenant,
                           current_setting('app.user_id', true) AS user_id
                    """)
                .SingleAsync(ct);
    }
}
