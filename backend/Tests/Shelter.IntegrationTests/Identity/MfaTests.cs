using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shelter.BuildingBlocks.Authorization;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>M2-4: TOTP enrollment, two-step login, recovery codes, enforcement for administrators and operators.</summary>
public sealed class MfaTests(PostgresFixture postgres)
{
    private const string StaffPath = "/api/platform/staff";
    private const string SpeciesPath = "/api/animals/species";
    private const string SetupPath = "/api/platform/session/mfa/setup";
    private const string EnablePath = "/api/platform/session/mfa/enable";
    private const string DisablePath = "/api/platform/session/mfa/disable";
    private const string LoginMfaPath = "/api/platform/session/login/mfa";
    private const string LogoutPath = "/api/platform/session/logout";

    [Fact]
    public async Task Administrator_without_mfa_is_limited_to_enrollment_until_enrolled()
    {
        var (userId, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);
        Assert.Equal("signedIn", await StatusOfAsync(await client.LoginAsync(email, TestAccounts.Password)));

        // Signed in, organization selected, but only the session and enrollment answer.
        var before = await SessionAsync(client);
        Assert.True(before.GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.False(before.GetProperty("user").GetProperty("mfaEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, StaffPath));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, SpeciesPath));

        var setup = await client.PostAsync(SetupPath);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var setupBody = await setup.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var key = setupBody.GetProperty("sharedKey").GetString()!;
        Assert.StartsWith("otpauth://totp/Shelter:", setupBody.GetProperty("authenticatorUri").GetString(), StringComparison.Ordinal);
        Assert.Contains($"secret={key}", setupBody.GetProperty("authenticatorUri").GetString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(EnablePath, new { code = "000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, StaffPath));

        var enable = await client.PostAsync(EnablePath, new { code = Totp.Code(key) });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var codes = await RecoveryCodesAsync(enable);
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct(StringComparer.Ordinal).Count());

        // Full access in this same session: enabling re-issued the cookie as signed in with MFA.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, StaffPath));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, SpeciesPath));
        var after = await SessionAsync(client);
        Assert.False(after.GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.True(after.GetProperty("user").GetProperty("mfaEnabled").GetBoolean());

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(SetupPath)).StatusCode);
        Assert.Equal(["LoginSucceeded", "MfaEnabled"], await SecurityEventsAsync(userId));
    }

    [Fact]
    public async Task Enrolled_account_signs_in_in_two_steps()
    {
        var (userId, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var (key, _) = await EnrollAsync(factory, email);

        using var client = new SessionClient(factory);
        Assert.Equal("mfaRequired", await StatusOfAsync(await client.LoginAsync(email, TestAccounts.Password)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetSessionAsync()).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(LoginMfaPath, new { code = "000000" })).StatusCode);
        var second = await client.PostAsync(LoginMfaPath, new { code = Totp.Code(key) });
        Assert.Equal("signedIn", await StatusOfAsync(second));

        Assert.False((await SessionAsync(client)).GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, StaffPath));

        // The re-issued antiforgery token works for the new principal.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(LogoutPath)).StatusCode);
        Assert.Equal(
            ["LoginSucceeded", "MfaEnabled", "LoggedOut", "LoginFailed", "LoginSucceeded", "LoggedOut"],
            await SecurityEventsAsync(userId));
    }

    [Fact]
    public async Task Second_step_without_a_verified_password_is_rejected()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);
        (await client.FetchAntiforgeryAsync()).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(LoginMfaPath, new { code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(LoginMfaPath, new { recoveryCode = "AAAAA-BBBBB" })).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        var (userId, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var (_, codes) = await EnrollAsync(factory, email);

        // Stored as hashes, never as the codes themselves.
        var stored = await StoredRecoveryCodesAsync(userId);
        Assert.Equal(10, stored.Split(';').Length);
        Assert.All(codes, code => Assert.DoesNotContain(code, stored, StringComparison.OrdinalIgnoreCase));

        using (var client = new SessionClient(factory))
        {
            Assert.Equal("mfaRequired", await StatusOfAsync(await client.LoginAsync(email, TestAccounts.Password)));
            Assert.Equal("signedIn", await StatusOfAsync(await client.PostAsync(LoginMfaPath, new { recoveryCode = codes[0].ToLowerInvariant() })));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, StaffPath));
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(LogoutPath)).StatusCode);
        }

        using (var client = new SessionClient(factory))
        {
            Assert.Equal("mfaRequired", await StatusOfAsync(await client.LoginAsync(email, TestAccounts.Password)));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(LoginMfaPath, new { recoveryCode = codes[0] })).StatusCode);
            Assert.Equal("signedIn", await StatusOfAsync(await client.PostAsync(LoginMfaPath, new { recoveryCode = codes[1] })));
        }

        var events = await SecurityEventsAsync(userId);
        Assert.Equal(2, events.Count(e => e == "RecoveryCodeUsed"));
        Assert.Equal(8, (await StoredRecoveryCodesAsync(userId)).Split(';').Length);
    }

    [Fact]
    public async Task Disabling_requires_a_code_and_restores_enforcement()
    {
        var (userId, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var (key, _) = await EnrollAsync(factory, email);

        using var client = new SessionClient(factory);
        await client.LoginAsync(email, TestAccounts.Password);
        Assert.Equal("signedIn", await StatusOfAsync(await client.PostAsync(LoginMfaPath, new { code = Totp.Code(key) })));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(DisablePath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(DisablePath, new { code = "000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(DisablePath, new { code = Totp.Code(key) })).StatusCode);

        // Still an administrator, now without MFA: back to enrollment only.
        Assert.True((await SessionAsync(client)).GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, StaffPath));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(DisablePath, new { code = Totp.Code(key) })).StatusCode);

        var events = await SecurityEventsAsync(userId);
        Assert.Equal(1, events.Count(e => e == "MfaEnabled"));
        Assert.Equal("MfaDisabled", events[^1]);
    }

    [Fact]
    public async Task Other_staff_are_not_required_to_use_mfa()
    {
        var (_, email) = await NewMemberAccountAsync(SystemRoles.Staff);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = new SessionClient(factory);
        Assert.Equal("signedIn", await StatusOfAsync(await client.LoginAsync(email, TestAccounts.Password)));

        Assert.False((await SessionAsync(client)).GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, SpeciesPath));
    }

    [Fact]
    public async Task Enforcement_follows_how_the_session_signed_in()
    {
        var organization = Guid.CreateVersion7();
        var administrator = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, SystemRoles.Administrator);
        var (operatorId, _) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        using var client = factory.CreateHttpsClient();

        client.SignInAs(administrator, organization, ("amr", "pwd"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, SpeciesPath));
        client.SignInAs(administrator, organization);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, SpeciesPath));

        // A platform operator must enroll too, with or without an organization.
        client.SignInAs(operatorId, null, (AuthorizationPolicies.PlatformOperatorClaim, "true"), ("amr", "pwd"));
        Assert.True((await client.GetFromJsonAsync<JsonElement>(new Uri("/api/platform/session", UriKind.Relative), TestContext.Current.CancellationToken))
            .GetProperty("mfaEnrollmentRequired").GetBoolean());
    }

    [Fact]
    public async Task Bypass_flag_is_ignored_outside_development()
    {
        var (_, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            Settings = new Dictionary<string, string?> { [MfaEnforcement.DevelopmentBypassKey] = "true" },
        };
        using var client = new SessionClient(factory);
        await client.LoginAsync(email, TestAccounts.Password);

        Assert.True((await SessionAsync(client)).GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(client, StaffPath));
    }

    [Fact]
    public async Task Bypass_flag_is_honored_in_development()
    {
        var (_, email) = await NewMemberAccountAsync(SystemRoles.Administrator);
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            EnvironmentName = "Development",
            Settings = new Dictionary<string, string?>
            {
                [MfaEnforcement.DevelopmentBypassKey] = "true",
                ["DevSeed:Enabled"] = "false",
            },
        };
        using var client = new SessionClient(factory);
        await client.LoginAsync(email, TestAccounts.Password);

        Assert.False((await SessionAsync(client)).GetProperty("mfaEnrollmentRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, StaffPath));
    }

    /// <summary>An account with a password and a single active membership, which login selects automatically.</summary>
    private async Task<(Guid Id, string Email)> NewMemberAccountAsync(string role)
    {
        var account = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, account.Id, roles: [role]);
        return account;
    }

    /// <summary>Enrolls the account in its own session, which ends with a logout.</summary>
    private static async Task<(string Key, IReadOnlyList<string> RecoveryCodes)> EnrollAsync(ShelterApiFactory factory, string email)
    {
        using var client = new SessionClient(factory);
        (await client.LoginAsync(email, TestAccounts.Password)).EnsureSuccessStatusCode();
        var setup = await client.PostAsync(SetupPath);
        var key = (await setup.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("sharedKey").GetString()!;
        var codes = await RecoveryCodesAsync((await client.PostAsync(EnablePath, new { code = Totp.Code(key) })).EnsureSuccessStatusCode());
        (await client.PostAsync(LogoutPath)).EnsureSuccessStatusCode();
        return (key, codes);
    }

    private static async Task<IReadOnlyList<string>> RecoveryCodesAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();

    private static async Task<string?> StatusOfAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("status").GetString();
    }

    private static async Task<JsonElement> SessionAsync(SessionClient client)
    {
        var response = await client.GetSessionAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static Task<HttpStatusCode> StatusAsync(SessionClient client, string path) => StatusAsync(client.Http, path);

    private async Task<string> StoredRecoveryCodesAsync(Guid userId)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT value FROM platform.user_token WHERE user_id = @id AND name = 'RecoveryCodes'", connection);
        command.Parameters.AddWithValue("id", userId);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private Task<List<string>> SecurityEventsAsync(Guid userId) =>
        TestSecurityEvents.ListAsync(postgres.Database.MigratorConnectionString, userId);
}
