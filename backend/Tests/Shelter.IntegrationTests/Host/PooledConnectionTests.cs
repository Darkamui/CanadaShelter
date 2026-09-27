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
/// M1-3: with a pool of one, tenant A's request and tenant B's request share one physical connection, and
/// nothing of A (rows or the tenant setting) is visible to B or to a later request without a tenant.
/// </summary>
public sealed class PooledConnectionTests(PostgresFixture postgres)
{
    private const string TenantHeader = "X-Tenant-Id";

    [Fact]
    public async Task Tenant_b_on_tenant_as_pooled_connection_sees_none_of_as_rows()
    {
        var singleConnection = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString)
        {
            MaxPoolSize = 1,
            ApplicationName = "pooled-connection-test", // own pool, not shared with other tests
        }.ConnectionString;
        await using var factory = new ShelterApiFactory(singleConnection, new PoolProbeModule()) { EnvironmentName = "Development" };
        using var client = await factory.CreateAntiforgeryClientAsync();
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var written = await SendAsync<Probe>(client, HttpMethod.Post, tenantA);
        var readByB = await SendAsync<Probe>(client, HttpMethod.Get, tenantB);
        var readWithoutTenant = await SendAsync<Probe>(client, HttpMethod.Get, tenantId: null);

        // Precondition: all three requests really shared one physical connection.
        Assert.True(written.Pid == readByB.Pid && written.Pid == readWithoutTenant.Pid, "setup: the pool did not reuse one connection");

        Assert.Equal(1, written.Count);
        Assert.Equal(0, readByB.Count);
        Assert.Equal(tenantB.ToString("D"), readByB.Tenant);
        Assert.Equal(0, readWithoutTenant.Count);
        Assert.True(string.IsNullOrEmpty(readWithoutTenant.Tenant), "the tenant setting outlived its transaction");
    }

    private static async Task<T> SendAsync<T>(HttpClient client, HttpMethod method, Guid? tenantId)
    {
        using var request = new HttpRequestMessage(method, new Uri("/api/test-pool/settings", UriKind.Relative));
        if (tenantId is { } id)
        {
            request.Headers.Add(TenantHeader, id.ToString("D"));
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private sealed record Probe(int Pid, int Count, string? Tenant);

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
                           current_setting('app.tenant_id', true) AS tenant
                    """)
                .SingleAsync(ct);
    }
}
