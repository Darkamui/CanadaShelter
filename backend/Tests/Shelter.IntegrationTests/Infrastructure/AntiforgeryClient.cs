using Microsoft.AspNetCore.Mvc.Testing;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>Clients for unsafe requests: module endpoints require the antiforgery double-submit (ADR 0018).</summary>
internal static class AntiforgeryClient
{
    /// <summary>
    /// An HTTPS client (the antiforgery cookie is Secure outside Development) with a cookie jar holding the
    /// antiforgery cookie and <c>X-XSRF-TOKEN</c> set on every request. Signed in as <paramref name="userId"/> through
    /// <see cref="TestAuthentication"/> when given, anonymous otherwise: the token is bound to that user.
    /// </summary>
    public static async Task<HttpClient> CreateAntiforgeryClientAsync(this ShelterApiFactory factory, Guid? userId = null, Guid? organizationId = null)
    {
        var client = factory.CreateHttpsClient(userId, organizationId);
        var response = await client.GetAsync(new Uri("/api/platform/session/antiforgery", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var token = response.Headers.GetValues("Set-Cookie")
            .Select(c => c.Split(';')[0])
            .Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal))["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
        return client;
    }

    /// <summary>An HTTPS client, signed in through <see cref="TestAuthentication"/> when given a user.</summary>
    public static HttpClient CreateHttpsClient(this ShelterApiFactory factory, Guid? userId = null, Guid? organizationId = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (userId is { } user)
        {
            client.SignInAs(user, organizationId);
        }

        return client;
    }
}
