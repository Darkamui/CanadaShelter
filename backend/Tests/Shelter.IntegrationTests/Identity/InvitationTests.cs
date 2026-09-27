using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shelter.BuildingBlocks.Communications;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>
/// M2-5: administrators invite staff by email; the link's secret alone finds the organization; accepting creates the
/// membership with the invited roles, once; an email match grants nothing without signing in as that account.
/// </summary>
public sealed class InvitationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string InvitationsPath = "/api/platform/invitations";
    private const string LookupPath = "/api/platform/invitations/lookup";
    private const string AcceptPath = "/api/platform/invitations/accept";
    private const string NewPassword = "une-phrase-de-passe-longue";

    private static readonly string[] Staff = ["staff"];
    private static readonly string[] Administrator = ["administrator"];
    private static readonly string[] Unknown = ["owner"];

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);

    [Fact]
    public async Task New_person_accepts_once_and_gets_the_invited_roles()
    {
        var (organization, admin) = await NewOrganizationAsync("Refuge des Monts");
        var email = NewEmail();

        var link = await InviteAsync(admin, organization, email, ["staff"]);
        var message = Assert.Single(_factory.Emails.SentTo(email));
        Assert.Equal("Invitation à rejoindre Refuge des Monts", message.Subject);
        Assert.StartsWith($"{ShelterApiFactory.PublicBaseUrl}/accept-invitation#token=", link, StringComparison.Ordinal);

        using var anonymous = await _factory.CreateAntiforgeryClientAsync();
        var token = TokenOf(link);
        var lookup = await ReadAsync(await anonymous.PostAsJsonAsync(LookupPath, new { token }, TestContext.Current.CancellationToken));
        Assert.Equal("Refuge des Monts", lookup.GetProperty("organizationName").GetString());
        Assert.Equal(email, lookup.GetProperty("email").GetString());
        Assert.False(lookup.GetProperty("accountExists").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, await AcceptAsync(anonymous, token, "Camille Tremblay", "court"));
        Assert.Equal(HttpStatusCode.NoContent, await AcceptAsync(anonymous, token, "Camille Tremblay", NewPassword));

        // The new account signs in and lands in the organization with the invited roles.
        using var session = new SessionClient(_factory);
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(email, NewPassword)).StatusCode);
        var current = await ReadAsync(await session.GetSessionAsync());
        Assert.Equal(organization, current.GetProperty("activeOrganizationId").GetGuid());
        Assert.Equal("Camille Tremblay", current.GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal(["staff"], await RolesAsync(organization, current.GetProperty("user").GetProperty("id").GetGuid()));

        // Single use: the same link is now unknown.
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, token, "Autre", NewPassword));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync(LookupPath, new { token }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Empty(await ListAsync(admin, organization));
    }

    [Fact]
    public async Task Existing_account_must_be_signed_in_as_itself()
    {
        var (organization, admin) = await NewOrganizationAsync();
        var (userId, email) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        var (otherId, _) = await TestAccounts.CreateAsync(postgres.AppConnectionString);

        // The account prefers French: its language wins over the invitation's.
        var token = TokenOf(await InviteAsync(admin, organization, email, ["read_only"], language: "en"));
        Assert.StartsWith("Invitation à rejoindre", Assert.Single(_factory.Emails.SentTo(email)).Subject, StringComparison.Ordinal);

        using var anonymous = await _factory.CreateAntiforgeryClientAsync();
        Assert.True((await ReadAsync(await anonymous.PostAsJsonAsync(LookupPath, new { token }, TestContext.Current.CancellationToken)))
            .GetProperty("accountExists").GetBoolean());

        // The email matches an account: a name and password do not take it over, nor does another account.
        Assert.Equal(HttpStatusCode.Forbidden, await AcceptAsync(anonymous, token, "Intrus", NewPassword));
        using var other = await _factory.CreateAntiforgeryClientAsync(otherId);
        Assert.Equal(HttpStatusCode.Forbidden, await AcceptAsync(other, token));
        Assert.Empty(await RolesAsync(organization, otherId));

        using var owner = await _factory.CreateAntiforgeryClientAsync(userId);
        Assert.Equal(HttpStatusCode.NoContent, await AcceptAsync(owner, token));
        Assert.Equal(["read_only"], await RolesAsync(organization, userId));
    }

    [Fact]
    public async Task Revoked_expired_and_replaced_links_are_rejected()
    {
        var (organization, admin) = await NewOrganizationAsync();
        using var anonymous = await _factory.CreateAntiforgeryClientAsync();

        // Revoked.
        var revokedEmail = NewEmail();
        var revoked = TokenOf(await InviteAsync(admin, organization, revokedEmail, ["staff"]));
        var revokedId = Assert.Single(await ListAsync(admin, organization)).GetProperty("id").GetGuid();
        using var adminClient = await _factory.CreateAntiforgeryClientAsync(admin, organization);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(new Uri($"{InvitationsPath}/{revokedId}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, revoked, "Nom", NewPassword));
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsync(new Uri($"{InvitationsPath}/{revokedId}/resend", UriKind.Relative), null, TestContext.Current.CancellationToken)).StatusCode);

        // Resent: the first link stops working, the new one works.
        var resentEmail = NewEmail();
        var first = TokenOf(await InviteAsync(admin, organization, resentEmail, ["staff"]));
        var resentId = Assert.Single(await ListAsync(admin, organization)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PostAsync(new Uri($"{InvitationsPath}/{resentId}/resend", UriKind.Relative), null, TestContext.Current.CancellationToken)).StatusCode);
        var second = TokenOf(LinkOf(_factory.Emails.SentTo(resentEmail)[^1]));
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, first, "Nom", NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, await AcceptAsync(anonymous, second, "Nom", NewPassword));

        // Expired.
        var expiredEmail = NewEmail();
        var expired = TokenOf(await InviteAsync(admin, organization, expiredEmail, ["staff"]));
        await ExpireAsync(organization, expiredEmail);
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, expired, "Nom", NewPassword));
        Assert.Equal("expired", Assert.Single(await ListAsync(admin, organization)).GetProperty("status").GetString());

        // An expired invitation can be replaced by a new one for the same email.
        Assert.Equal(HttpStatusCode.NoContent, await AcceptAsync(anonymous, TokenOf(await InviteAsync(admin, organization, expiredEmail, ["staff"])), "Nom", NewPassword));

        // Malformed secrets read as unknown.
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, "pas-un-jeton", "Nom", NewPassword));
        Assert.Equal(HttpStatusCode.NotFound, await AcceptAsync(anonymous, null, "Nom", NewPassword));
    }

    [Fact]
    public async Task A_link_only_reaches_its_own_organization()
    {
        var (organizationA, adminA) = await NewOrganizationAsync("Refuge A");
        var (organizationB, adminB) = await NewOrganizationAsync("Refuge B");
        var email = NewEmail();
        var tokenB = TokenOf(await InviteAsync(adminB, organizationB, email, ["administrator"]));
        var invitationB = Assert.Single(await ListAsync(adminB, organizationB)).GetProperty("id").GetGuid();

        // Organization A's administrator neither sees nor manages B's invitation.
        Assert.Empty(await ListAsync(adminA, organizationA));
        using var clientA = await _factory.CreateAntiforgeryClientAsync(adminA, organizationA);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync(new Uri($"{InvitationsPath}/{invitationB}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync(new Uri($"{InvitationsPath}/{invitationB}/resend", UriKind.Relative), null, TestContext.Current.CancellationToken)).StatusCode);

        // Accepted from a session whose active organization is A, the membership is still in B only.
        var (userId, userEmail) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organizationA, userId, roles: ["staff"]);
        var tokenForUser = TokenOf(await InviteAsync(adminB, organizationB, userEmail, ["read_only"]));
        using var userInA = await _factory.CreateAntiforgeryClientAsync(userId, organizationA);
        Assert.Equal(HttpStatusCode.NoContent, await AcceptAsync(userInA, tokenForUser));
        Assert.Equal(["staff"], await RolesAsync(organizationA, userId));
        Assert.Equal(["read_only"], await RolesAsync(organizationB, userId));

        using var anonymous = await _factory.CreateAntiforgeryClientAsync();
        Assert.Equal("Refuge B", (await ReadAsync(await anonymous.PostAsJsonAsync(LookupPath, new { token = tokenB }, TestContext.Current.CancellationToken)))
            .GetProperty("organizationName").GetString());
    }

    [Fact]
    public async Task Create_validates_and_refuses_duplicates_and_members()
    {
        var (organization, admin) = await NewOrganizationAsync();
        using var adminClient = await _factory.CreateAntiforgeryClientAsync(admin, organization);
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(adminClient, new { email = "pas un courriel", roles = Staff }));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(adminClient, new { email, roles = Unknown }));
        Assert.Equal(HttpStatusCode.BadRequest, await CreateAsync(adminClient, new { email, roles = Staff, language = "es" }));

        Assert.Equal(HttpStatusCode.Created, await CreateAsync(adminClient, new { email, roles = Staff, language = "en" }));
        Assert.Equal("Invitation to join Refuge test", Assert.Single(_factory.Emails.SentTo(email)).Subject);
        Assert.Equal(HttpStatusCode.Conflict, await CreateAsync(adminClient, new { email = email.ToUpperInvariant(), roles = Staff }));

        var (memberId, memberEmail) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        await TestMemberships.AddAsync(postgres.AppConnectionString, organization, memberId, roles: ["staff"]);
        Assert.Equal(HttpStatusCode.Conflict, await CreateAsync(adminClient, new { email = memberEmail, roles = Staff }));
    }

    [Fact]
    public async Task Staff_without_manage_permission_cannot_invite()
    {
        var (organization, _) = await NewOrganizationAsync();
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, "staff");
        using var client = await _factory.CreateAntiforgeryClientAsync(staff, organization);

        Assert.Equal(HttpStatusCode.Forbidden, await CreateAsync(client, new { email = NewEmail(), roles = Administrator }));
        Assert.Empty(_factory.Emails.Sent);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static string NewEmail() => $"invite-{Guid.NewGuid():N}@example.test";

    private static string TokenOf(string link) => link[(link.IndexOf("#token=", StringComparison.Ordinal) + "#token=".Length)..];

    private static string LinkOf(EmailMessage message) =>
        message.TextBody.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith(ShelterApiFactory.PublicBaseUrl, StringComparison.Ordinal));

    private async Task<(Guid Organization, Guid Admin)> NewOrganizationAsync(string name = "Refuge test")
    {
        var organization = await TestOrganizations.CreateAsync(postgres.Database.PlatformAdminConnectionString, name);
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, organization, "administrator");
        return (organization, admin);
    }

    /// <summary>Invites and returns the link from the captured email.</summary>
    private async Task<string> InviteAsync(Guid admin, Guid organization, string email, string[] roles, string? language = null)
    {
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, organization);
        Assert.Equal(HttpStatusCode.Created, await CreateAsync(client, new { email, roles, language }));
        return LinkOf(_factory.Emails.SentTo(email)[^1]);
    }

    private static async Task<HttpStatusCode> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(InvitationsPath, body, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> AcceptAsync(HttpClient client, string? token, string? displayName = null, string? password = null)
    {
        using var response = await client.PostAsJsonAsync(AcceptPath, new { token, displayName, password }, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private async Task<List<JsonElement>> ListAsync(Guid admin, Guid organization)
    {
        using var client = _factory.CreateHttpsClient(admin, organization);
        return (await client.GetFromJsonAsync<List<JsonElement>>(new Uri(InvitationsPath, UriKind.Relative), TestContext.Current.CancellationToken))!;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The account's role keys in the organization (empty without a membership).</summary>
    private async Task<List<string>> RolesAsync(Guid organization, Guid userId)
    {
        var roles = await InOrganizationAsync(
            organization,
            "SELECT role_keys FROM platform.staff_membership WHERE user_id = @user",
            command => command.Parameters.AddWithValue("user", userId),
            command => command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        return roles is string[] keys ? [.. keys] : [];
    }

    /// <summary>Moves the open invitation for the email, and its secret, past their expiry.</summary>
    private Task<object?> ExpireAsync(Guid organization, string email) =>
        InOrganizationAsync<object?>(
            organization,
            """
            UPDATE platform.invitation_token SET expires_at = now() - interval '1 minute'
            WHERE invitation_id IN (SELECT id FROM platform.staff_invitation WHERE email = @email AND accepted_at IS NULL AND revoked_at IS NULL);
            UPDATE platform.staff_invitation SET expires_at = now() - interval '1 minute'
            WHERE email = @email AND accepted_at IS NULL AND revoked_at IS NULL;
            """,
            command => command.Parameters.AddWithValue("email", email),
            async command => await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

    /// <summary>Runs SQL as the migrator inside the organization: RLS is forced, even for the table owner.</summary>
    private async Task<T> InOrganizationAsync<T>(Guid organization, string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlCommand, Task<T>> run)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            scope.Parameters.AddWithValue("tenant", organization.ToString("D"));
            await scope.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        parameters(command);
        var result = await run(command);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return result;
    }
}
