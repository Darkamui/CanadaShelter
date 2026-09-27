using Microsoft.AspNetCore.Mvc.Testing;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>Clients for unsafe requests: module endpoints require the antiforgery double-submit (ADR 0018).</summary>
internal static class AntiforgeryClient
{
    /// <summary>
    /// An HTTPS client (the antiforgery cookie is Secure outside Development) with a cookie jar holding the
    /// antiforgery cookie and <c>X-XSRF-TOKEN</c> set on every request. Anonymous: the token is bound to no user.
    /// </summary>
    public static async Task<HttpClient> CreateAntiforgeryClientAsync(this ShelterApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(new Uri("/api/platform/session/antiforgery", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var token = response.Headers.GetValues("Set-Cookie")
            .Select(c => c.Split(';')[0])
            .Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal))["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
        return client;
    }
}
