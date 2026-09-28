using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Identity;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.RateLimiting;

/// <summary>
/// M2-8: anonymous endpoints that take a password, code or secret share one fixed window per client IP; over the limit
/// they answer 429 with a problem and <c>Retry-After</c>.
/// </summary>
public sealed class RateLimitingTests(PostgresFixture postgres)
{
    /// <summary>Anonymous endpoints that take nothing secret; every other anonymous endpoint is rate limited.</summary>
    private static readonly HashSet<string> Unlimited = new(StringComparer.Ordinal)
    {
        "GET /api/platform/ping",
        "GET /api/platform/session/antiforgery",
        "* /health/live",
        "* /health/ready",
    };

    [Fact]
    public async Task Every_other_anonymous_endpoint_is_rate_limited()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => (Name: $"{method} /{e.RoutePattern.RawText!.TrimStart('/')}", Endpoint: e)))
            .Where(e => !Unlimited.Contains(e.Name))
            .ToList();

        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, e => Assert.Equal(RateLimitPolicies.Anonymous, e.Endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Fact]
    public async Task Over_the_limit_the_anonymous_endpoints_answer_429()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            Settings = new Dictionary<string, string?>
            {
                ["RateLimiting:Anonymous:PermitLimit"] = "3",
                ["RateLimiting:Anonymous:Window"] = "00:10:00",
            },
        };
        using var client = new SessionClient(factory);
        await client.FetchAntiforgeryAsync();
        var email = $"nobody-{Guid.NewGuid():N}@example.test";

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, "wrong password")).StatusCode);
        }

        var limited = await client.LoginAsync(email, "wrong password");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());

        // One window for all of them; the antiforgery endpoint is not limited.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/platform/invitations/lookup", new { token = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.FetchAntiforgeryAsync()).StatusCode);
    }
}
