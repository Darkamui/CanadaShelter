using System.Globalization;
using System.Net;
using Npgsql;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>
/// M2-5: forgot/reset password by email, without revealing which emails have an account. M2-8: the email leaves from
/// a background job, so this class runs a job server.
/// </summary>
[Collection(HangfireTests.Name)]
public sealed class PasswordResetTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string ForgotPath = "/api/platform/session/password/forgot";
    private const string ResetPath = "/api/platform/session/password/reset";
    private const string NewPassword = "une-toute-nouvelle-phrase";

    private readonly DateTime _started = DateTime.UtcNow.AddSeconds(-1);
    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString) { JobServerEnabled = true };

    [Fact]
    public async Task Reset_link_sets_a_new_password_once()
    {
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        using var client = new SessionClient(_factory);
        await client.FetchAntiforgeryAsync();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync(ForgotPath, new { email = email.ToUpperInvariant() })).StatusCode);
        var message = Assert.Single(await _factory.Emails.WaitForAsync(email));
        Assert.Equal("Réinitialisation de votre mot de passe", message.Subject);
        var (linkUser, token) = ParseLink(message.TextBody);
        Assert.Equal(userId, linkUser);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(ResetPath, new { userId, token = token + "x", newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(ResetPath, new { userId, token, newPassword = "court" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(ResetPath, new { userId, token, newPassword = NewPassword })).StatusCode);

        // The security stamp rotated: the link is spent.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(ResetPath, new { userId, token, newPassword = NewPassword + "2" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email, TestAccounts.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email, NewPassword)).StatusCode);
        Assert.Contains("PasswordChanged", await TestSecurityEvents.ListAsync(postgres.Database.MigratorConnectionString, userId));
    }

    [Fact]
    public async Task Forgot_answers_the_same_for_unknown_emails()
    {
        var (_, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        using var client = new SessionClient(_factory);
        await client.FetchAntiforgeryAsync();

        var known = await client.PostAsync(ForgotPath, new { email });
        var unknown = await client.PostAsync(ForgotPath, new { email = $"nobody-{Guid.NewGuid():N}@example.test" });
        var empty = await client.PostAsync(ForgotPath, new { });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(known.StatusCode, empty.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Every request enqueues a job; only the known account gets an email.
        Assert.Single(await _factory.Emails.WaitForAsync(email));
        Assert.Equal(3, await EnqueuedResetJobsAsync());
        Assert.Single(_factory.Emails.Sent);
    }

    [Fact]
    public async Task Reset_for_an_unknown_account_gets_the_generic_answer()
    {
        using var client = new SessionClient(_factory);
        await client.FetchAntiforgeryAsync();

        var response = await client.PostAsync(ResetPath, new { userId = Guid.CreateVersion7(), token = "abc", newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    // Hangfire (tables owned by the runtime role) stores the job's type and arguments; this factory is the only one enqueueing reset jobs in the class.
    private async Task<int> EnqueuedResetJobsAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM hangfire.job WHERE invocationdata::text LIKE '%SendPasswordResetEmailJob%' AND createdat > @since", connection);
        command.Parameters.AddWithValue("since", _started);
        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    private static (Guid UserId, string Token) ParseLink(string body)
    {
        var link = body.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith(ShelterApiFactory.PublicBaseUrl + "/reset-password#", StringComparison.Ordinal));
        var values = link[(link.IndexOf('#', StringComparison.Ordinal) + 1)..]
            .Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);
        return (Guid.Parse(values["user"]), values["token"]);
    }
}
