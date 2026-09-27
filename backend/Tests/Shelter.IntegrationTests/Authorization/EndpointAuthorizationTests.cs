using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Modules;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Authorization;

/// <summary>
/// M2-3: default deny is structural. Every endpoint declares a permission, the session policy or the operator policy,
/// or is on the anonymous allowlist; every permission endpoint turns away a member without it; an endpoint that
/// declares nothing still needs a member of the active organization (fallback policy).
/// </summary>
public sealed partial class EndpointAuthorizationTests(PostgresFixture postgres) : IAsyncDisposable
{
    /// <summary>The only anonymous endpoints (ADR 0018). Adding one is a reviewed change to this list.</summary>
    private static readonly HashSet<string> AnonymousAllowlist = new(StringComparer.Ordinal)
    {
        "GET /api/platform/ping",
        "GET /api/platform/session/antiforgery",
        "POST /api/platform/session/login",
        "POST /api/platform/session/login/mfa",
        "* /health/live",
        "* /health/ready",
    };

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);

    [Fact]
    public void Every_endpoint_declares_who_may_call_it()
    {
        var catalog = _factory.Services.GetRequiredService<PermissionCatalog>();
        var undeclared = new List<string>();
        foreach (var (name, endpoint) in Endpoints(_factory))
        {
            var metadata = endpoint.Metadata;
            var declared = metadata.GetMetadata<PermissionEndpointMetadata>() is { } permission
                ? catalog.Contains(permission.Permission) || Fail($"{name}: unknown permission '{permission.Permission}'")
                : metadata.GetMetadata<SessionEndpointMetadata>() is not null
                    || metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == AuthorizationPolicies.PlatformOperator)
                    || (metadata.GetMetadata<IAllowAnonymous>() is not null && AnonymousAllowlist.Contains(name));
            if (!declared)
            {
                undeclared.Add(name);
            }
        }

        Assert.Empty(undeclared);
    }

    [Fact]
    public void Allowlisted_endpoints_exist_and_are_anonymous()
    {
        var anonymous = Endpoints(_factory)
            .Where(e => e.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Subset(anonymous, AnonymousAllowlist);
    }

    [Fact]
    public async Task Every_permission_endpoint_denies_a_member_without_the_permission()
    {
        var organization = Guid.CreateVersion7();
        var noRoles = Guid.CreateVersion7();
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, noRoles, roles: []);
        using var client = await _factory.CreateAntiforgeryClientAsync(noRoles, organization);

        var permissionEndpoints = Endpoints(_factory).Where(e => e.Endpoint.Metadata.GetMetadata<PermissionEndpointMetadata>() is not null).ToList();
        Assert.NotEmpty(permissionEndpoints);

        var allowed = new List<string>();
        foreach (var (name, endpoint) in permissionEndpoints)
        {
            var method = endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods[0];
            var path = RouteParameter().Replace(endpoint.RoutePattern.RawText!, _ => Guid.CreateVersion7().ToString("D"));
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                allowed.Add($"{name}: {(int)response.StatusCode}");
            }
        }

        Assert.Empty(allowed);
    }

    [Fact]
    public async Task Endpoint_without_a_declaration_is_denied_to_everyone_but_members()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString, new UndeclaredModule());
        var organization = Guid.CreateVersion7();
        var member = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization);
        var outsider = Guid.CreateVersion7();
        var path = new Uri("/api/test-undeclared/probe", UriKind.Relative);

        using var anonymous = factory.CreateHttpsClient();
        using var signedInWithoutOrganization = factory.CreateHttpsClient(member);
        using var notAMember = factory.CreateHttpsClient(outsider, organization);
        using var memberClient = factory.CreateHttpsClient(member, organization);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await signedInWithoutOrganization.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await notAMember.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static bool Fail(string message) => throw new Xunit.Sdk.XunitException(message);

    private static IEnumerable<(string Name, RouteEndpoint Endpoint)> Endpoints(ShelterApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => ($"{method} /{e.RoutePattern.RawText!.TrimStart('/')}", e)));

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();

    /// <summary>Test-only module with an endpoint that declares nothing.</summary>
    private sealed class UndeclaredModule : IModule
    {
        public string RoutePrefix => "test-undeclared";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapGet("/probe", () => Results.Ok());
    }
}
