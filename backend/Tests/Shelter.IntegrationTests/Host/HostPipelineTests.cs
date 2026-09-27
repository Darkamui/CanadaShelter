using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Modules;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Host;

public sealed class HostPipelineTests(PostgresFixture postgres)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    [Fact]
    public async Task Ping_returns_ok()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/platform/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_is_healthy_without_db()
    {
        await using var factory = new ShelterApiFactory(ShelterApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_is_healthy_with_db()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_is_unhealthy_when_db_unreachable()
    {
        await using var factory = new ShelterApiFactory(ShelterApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Unhealthy", body);
    }

    [Fact]
    public async Task Unknown_route_is_denied_to_anonymous_callers()
    {
        // Default deny covers unmatched routes too: the fallback policy runs before "not found".
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var organization = Guid.CreateVersion7();
        var member = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization);
        using var client = factory.CreateHttpsClient(member, organization);

        var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_without_stack()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString, new ThrowingModule());
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/test-throw/boom", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains(CorrelationHeader));
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(ThrowingModule.SecretMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Correlation_id_is_echoed()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/platform/ping", UriKind.Relative));
        request.Headers.Add(CorrelationHeader, "abc-123");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("abc-123", Assert.Single(response.Headers.GetValues(CorrelationHeader)));
    }

    [Fact]
    public async Task Malformed_correlation_id_is_replaced()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/platform/ping", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, "bad value\twith spaces");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = Assert.Single(response.Headers.GetValues(CorrelationHeader));
        Assert.NotEqual("bad value\twith spaces", echoed);
        Assert.Matches("^[0-9a-f]{32}$", echoed);
    }

    [Fact]
    public async Task No_raw_data_source_is_registered()
    {
        // A raw NpgsqlDataSource would let module code bypass the tenant filter and tenant transaction.
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);

        Assert.Null(factory.Services.GetService<NpgsqlDataSource>());
    }

    /// <summary>Test-only module whose endpoint throws, to exercise the exception handler.</summary>
    private sealed class ThrowingModule : IModule
    {
        public const string SecretMessage = "sensitive-detail-that-must-not-leak";

        public string RoutePrefix => "test-throw";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapGet("/boom", () => { throw new InvalidOperationException(SecretMessage); }).AllowAnonymous();
    }
}
