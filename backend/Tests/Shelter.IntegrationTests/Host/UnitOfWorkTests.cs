using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Host;

/// <summary>
/// M1-2: a tenant request runs in one transaction with <c>SET LOCAL app.tenant_id</c>, committed on success and
/// rolled back on an error status. Raw SQL on purpose: no EF filter or stamping, only the database scopes it.
/// </summary>
public sealed class UnitOfWorkTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string TenantHeader = "X-Tenant-Id";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString, new UnitOfWorkProbeModule()) { EnvironmentName = "Development" };
    private readonly Guid _tenant = Guid.CreateVersion7();

    [Fact]
    public async Task Request_runs_with_the_tenant_set_locally()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/test-uow/tenant");

        response.EnsureSuccessStatusCode();
        Assert.Equal(_tenant.ToString("D"), await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Success_status_commits()
    {
        using var write = await SendAsync(HttpMethod.Post, "/api/test-uow/settings/committed?status=200");
        write.EnsureSuccessStatusCode();

        Assert.Equal(1, await CountAsync("committed"));
    }

    [Fact]
    public async Task Error_status_rolls_back()
    {
        using var write = await SendAsync(HttpMethod.Post, "/api/test-uow/settings/rolled-back?status=400");
        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);

        Assert.Equal(0, await CountAsync("rolled-back"));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<int> CountAsync(string key)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/test-uow/settings/{key}/count");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        using var client = await _factory.CreateAntiforgeryClientAsync();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add(TenantHeader, _tenant.ToString("D"));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Test-only module reading and writing <c>platform.tenant_setting</c> with raw SQL.</summary>
    private sealed class UnitOfWorkProbeModule : IModule
    {
        public string RoutePrefix => "test-uow";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/tenant", async (ShelterDbContext db, CancellationToken ct) =>
                Results.Json(await db.Database
                    .SqlQuery<string>($"SELECT current_setting('app.tenant_id', true) AS \"Value\"")
                    .SingleAsync(ct)));

            endpoints.MapPost("/settings/{key}", async (string key, int status, ShelterDbContext db, CancellationToken ct) =>
            {
                await db.Database.ExecuteSqlAsync(
                    $"INSERT INTO platform.tenant_setting (id, tenant_id, key, value) VALUES (gen_random_uuid(), platform.current_tenant_id(), {key}, 'x')",
                    ct);
                return Results.StatusCode(status);
            });

            endpoints.MapGet("/settings/{key}/count", async (string key, ShelterDbContext db, CancellationToken ct) =>
                Results.Json(await db.Database
                    .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM platform.tenant_setting WHERE key = {key}")
                    .SingleAsync(ct)));
        }
    }
}
