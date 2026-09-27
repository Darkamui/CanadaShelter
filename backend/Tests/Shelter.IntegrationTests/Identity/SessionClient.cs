using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>
/// An HTTPS client with a cookie jar that speaks the session API's antiforgery double-submit: it keeps the
/// readable <c>XSRF-TOKEN</c> cookie and echoes it in <c>X-XSRF-TOKEN</c> on unsafe requests.
/// </summary>
internal sealed class SessionClient : IDisposable
{
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    private string? _xsrfToken;

    public SessionClient(ShelterApiFactory factory)
    {
        // Secure cookies are only sent back over HTTPS.
        Http = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public HttpClient Http { get; }

    public async Task<HttpResponseMessage> FetchAntiforgeryAsync()
    {
        var response = await Http.GetAsync(new Uri("/api/platform/session/antiforgery", UriKind.Relative), TestContext.Current.CancellationToken);
        Capture(response);
        return response;
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        if (_xsrfToken is null)
        {
            (await FetchAntiforgeryAsync()).EnsureSuccessStatusCode();
        }

        return await PostAsync("/api/platform/session/login", new { email, password });
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null, bool withXsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body ?? new { }),
        };
        if (withXsrf && _xsrfToken is not null)
        {
            request.Headers.Add(XsrfHeader, _xsrfToken);
        }

        var response = await Http.SendAsync(request, TestContext.Current.CancellationToken);
        Capture(response);
        return response;
    }

    public Task<HttpResponseMessage> GetSessionAsync() =>
        Http.GetAsync(new Uri("/api/platform/session", UriKind.Relative), TestContext.Current.CancellationToken);

    public void Dispose() => Http.Dispose();

    private void Capture(HttpResponseMessage response)
    {
        var token = SetCookies(response)
            .Select(c => c.Split(';')[0])
            .Where(c => c.StartsWith(XsrfCookie + "=", StringComparison.Ordinal))
            .Select(c => Uri.UnescapeDataString(c[(XsrfCookie.Length + 1)..]))
            .LastOrDefault();
        if (token is not null)
        {
            _xsrfToken = token;
        }
    }

    public static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
}

/// <summary>Creates sign-in accounts directly in the database (the runtime role may insert accounts).</summary>
internal static class TestAccounts
{
    public const string Password = "correct-horse-battery";

    public static async Task<(Guid Id, string Email)> CreateAsync(string connectionString, string password = Password)
    {
        var id = Guid.CreateVersion7();
        var email = $"user-{id:N}@example.test";

        // The default hasher ignores the user argument.
        var hash = new PasswordHasher<object>().HashPassword(new object(), password);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO platform.user_account
                (id, display_name, preferred_language, is_platform_operator, created_at, user_name, normalized_user_name,
                 email, normalized_email, email_confirmed, password_hash, security_stamp, concurrency_stamp,
                 phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES
                (@id, 'Test User', 'fr', false, now(), @email, upper(@email),
                 @email, upper(@email), true, @hash, @stamp, @concurrency,
                 false, false, true, 0)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString("N").ToUpperInvariant());
        command.Parameters.AddWithValue("concurrency", Guid.NewGuid().ToString());
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return (id, email);
    }
}
