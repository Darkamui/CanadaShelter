using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only authentication (M2-2): a request with <see cref="UserHeader"/> is signed in as that account, with
/// <see cref="OrganizationHeader"/> as its active organization claim. Requests without it fall through to the real
/// session cookie, so cookie-login tests run unchanged. The claim is only a claim: the Host still re-checks it
/// against an active membership, exactly as for a cookie.
/// </summary>
internal static class TestAuthentication
{
    public const string UserHeader = "X-Test-User";
    public const string OrganizationHeader = "X-Test-Organization";

    private const string TestScheme = "Test";
    private const string SelectorScheme = "TestOrCookie";

    // PlatformClaims.ActiveOrganization is internal to the Platform module.
    private const string ActiveOrganizationClaim = "shelter:org";

    public static void AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestScheme, _ => { })
            .AddPolicyScheme(SelectorScheme, SelectorScheme, options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(UserHeader) ? TestScheme : IdentityConstants.ApplicationScheme);

        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = SelectorScheme;
            options.DefaultAuthenticateScheme = SelectorScheme;
            options.DefaultChallengeScheme = SelectorScheme;
            options.DefaultForbidScheme = SelectorScheme;
        });
    }

    /// <summary>Headers that sign a request in as <paramref name="userId"/>, optionally with an active organization.</summary>
    public static void SignInAs(this HttpClient client, Guid userId, Guid? organizationId = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Remove(UserHeader);
        client.DefaultRequestHeaders.Remove(OrganizationHeader);
        client.DefaultRequestHeaders.Add(UserHeader, userId.ToString("D"));
        if (organizationId is { } organization)
        {
            client.DefaultRequestHeaders.Add(OrganizationHeader, organization.ToString("D"));
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers[UserHeader], out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Malformed test user."));
            }

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString("D")) };
            if (Guid.TryParse(Request.Headers[OrganizationHeader], out var organizationId))
            {
                claims.Add(new Claim(ActiveOrganizationClaim, organizationId.ToString("D")));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestScheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestScheme)));
        }
    }
}

/// <summary>Creates staff memberships directly, as the runtime role inside the organization (RLS applies).</summary>
internal static class TestMemberships
{
    /// <summary>
    /// A membership of <paramref name="userId"/> in <paramref name="organizationId"/>. Memberships reference the
    /// account by ID only, so a test user needs no account row unless it signs in with a password.
    /// </summary>
    public static async Task AddAsync(string connectionString, Guid organizationId, Guid userId, string status = "Active")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('app.tenant_id', @tenant, true);
            INSERT INTO platform.staff_membership (id, tenant_id, user_id, status, created_at)
            VALUES (@id, @tenant::uuid, @user, @status, now());
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant", organizationId.ToString("D"));
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A new account ID with an active membership in <paramref name="organizationId"/>.</summary>
    public static async Task<Guid> NewMemberAsync(string connectionString, Guid organizationId)
    {
        var userId = Guid.CreateVersion7();
        await AddAsync(connectionString, organizationId, userId);
        return userId;
    }
}
