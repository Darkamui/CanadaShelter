using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>M2-1: cookie login, logout, session, lockout, antiforgery and cookie flags.</summary>
public sealed class SessionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Login_then_session_then_logout()
    {
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);

        var login = await client.LoginAsync(email, TestAccounts.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("signedIn", loginBody.GetProperty("status").GetString());

        var session = await client.GetSessionAsync();
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var user = (await session.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("user");
        Assert.Equal(userId, user.GetProperty("id").GetGuid());
        Assert.Equal("fr", user.GetProperty("preferredLanguage").GetString());

        // The antiforgery token was reissued for the signed-in user; logout uses it.
        var logout = await client.PostAsync("/api/platform/session/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetSessionAsync()).StatusCode);
        Assert.Equal(
            ["LoginSucceeded", "LoggedOut"],
            await SecurityEventsAsync(userId));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_response()
    {
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);

        var wrongPassword = await client.LoginAsync(email, "not-the-password");
        var unknownEmail = await client.LoginAsync($"nobody-{Guid.NewGuid():N}@example.test", "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(await ComparableProblemAsync(wrongPassword), await ComparableProblemAsync(unknownEmail));
        Assert.DoesNotContain(SessionClient.SetCookies(wrongPassword), c => c.Contains("shelter-session", StringComparison.Ordinal));
        Assert.Equal(["LoginFailed"], await SecurityEventsAsync(userId));
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_hide_it()
    {
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, "not-the-password")).StatusCode);
        }

        // Even the right password is refused while locked, with the same answer as a wrong one.
        var locked = await client.LoginAsync(email, TestAccounts.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetSessionAsync()).StatusCode);

        var events = await SecurityEventsAsync(userId);
        Assert.Equal(4, events.Count(e => e == "LoginFailed"));
        Assert.Equal(2, events.Count(e => e == "LockedOut"));
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_rejected()
    {
        var (_, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);
        await client.FetchAntiforgeryAsync();

        var response = await client.PostAsync("/api/platform/session/login", new { email, password = TestAccounts.Password }, withXsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetSessionAsync()).StatusCode);
    }

    [Fact]
    public async Task Logout_without_antiforgery_token_is_rejected()
    {
        var (_, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);
        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();

        var response = await client.PostAsync("/api/platform/session/logout", withXsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetSessionAsync()).StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_host_prefixed_secure_httponly_strict()
    {
        var (_, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);

        var login = await client.LoginAsync(email, TestAccounts.Password);

        var cookie = Assert.Single(SessionClient.SetCookies(login), c => c.StartsWith("__Host-shelter-session=", StringComparison.Ordinal));
        var attributes = cookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=strict", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain(attributes, a => a.StartsWith("domain=", StringComparison.Ordinal));

        // Session cookie: no persistent expiry; the 8-hour sliding lifetime lives in the ticket.
        Assert.DoesNotContain(attributes, a => a.StartsWith("expires=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Anonymous_session_request_is_401_problem_details()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);

        var response = await client.GetSessionAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Security_stamp_change_ends_the_session()
    {
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            ConfigureServices = services => services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero),
        };
        using var client = new SessionClient(factory);
        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetSessionAsync()).StatusCode);

        // What a password or MFA change does.
        await using (var connection = new NpgsqlConnection(postgres.AppConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("UPDATE platform.user_account SET security_stamp = 'ROTATED' WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", userId);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetSessionAsync()).StatusCode);
    }

    private static async Task<string> ComparableProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        problem.Remove("traceId");
        problem.Remove("correlationId");
        return JsonSerializer.Serialize(problem.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary());
    }

    /// <summary>The account's security events, oldest first. The runtime role cannot read them; the migrator can.</summary>
    private async Task<List<string>> SecurityEventsAsync(Guid userId)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT type FROM platform.security_event WHERE user_id = @id ORDER BY occurred_at, id", connection);
        command.Parameters.AddWithValue("id", userId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var types = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }
}
