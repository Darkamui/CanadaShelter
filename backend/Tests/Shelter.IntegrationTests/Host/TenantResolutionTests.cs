using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Host;

/// <summary>M1-1: the development tenant header is honoured in Development only; elsewhere no tenant is resolved.</summary>
public sealed class TenantResolutionTests(PostgresFixture postgres)
{
    private const string TenantHeader = "X-Tenant-Id";

    [Theory]
    [InlineData("Test")]
    [InlineData("Production")]
    public async Task Tenant_header_is_ignored_outside_development(string environment)
    {
        var tenant = await ResolvedTenantAsync(environment, Guid.CreateVersion7().ToString("D"));

        Assert.Equal("", tenant);
    }

    [Fact]
    public async Task Tenant_header_is_resolved_in_development()
    {
        var tenantId = Guid.CreateVersion7().ToString("D");

        var tenant = await ResolvedTenantAsync("Development", tenantId);

        Assert.Equal(tenantId, tenant);
    }

    [Fact]
    public async Task Malformed_tenant_header_resolves_no_tenant_in_development()
    {
        var tenant = await ResolvedTenantAsync("Development", "not-a-guid");

        Assert.Equal("", tenant);
    }

    private async Task<string?> ResolvedTenantAsync(string environment, string headerValue)
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString, new TenantEchoModule()) { EnvironmentName = environment };
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/test-tenant/current", UriKind.Relative));
        request.Headers.Add(TenantHeader, headerValue);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
    }

    /// <summary>Test-only module that returns the request's resolved tenant ("" when none).</summary>
    private sealed class TenantEchoModule : IModule
    {
        public string RoutePrefix => "test-tenant";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapGet("/current", (ITenantContext tenant) => Results.Json(tenant.TenantId?.ToString("D") ?? ""));
    }
}
