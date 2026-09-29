using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Identity;

/// <summary>
/// M2-3: administrators list the organization's staff, change roles, suspend and reactivate; changes are audited and
/// take effect on the next request; the last active administrator stays; other organizations' memberships are not found.
/// </summary>
public sealed class StaffManagementTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string StaffPath = "/api/platform/staff";
    private const string SpeciesPath = "/api/animals/species";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organization = Guid.CreateVersion7();

    [Fact]
    public async Task List_shows_only_the_organizations_staff()
    {
        var (adminId, _) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        var (colleagueId, colleagueEmail) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        var (outsiderId, _) = await TestAccounts.CreateAsync(postgres.AppConnectionString);
        var adminMembership = await AddAsync(adminId, "administrator");
        var colleagueMembership = await AddAsync(colleagueId, "read_only");
        await TestMemberships.AddAsync(postgres.AppConnectionString, Guid.CreateVersion7(), outsiderId, roles: ["administrator"]);
        using var client = _factory.CreateHttpsClient(adminId, _organization);

        var staff = await client.GetFromJsonAsync<List<StaffMember>>(new Uri(StaffPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(staff);
        Assert.Equal(new[] { adminMembership, colleagueMembership }.Order(), staff.Select(s => s.MembershipId).Order());
        var colleague = Assert.Single(staff, s => s.UserId == colleagueId);
        Assert.Equal(colleagueEmail, colleague.Email);
        Assert.Equal("Active", colleague.Status);
        Assert.Equal(["read_only"], colleague.Roles);
    }

    [Fact]
    public async Task Role_change_is_audited_and_applies_on_the_next_request()
    {
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "administrator");
        var colleague = Guid.CreateVersion7();
        var colleagueMembership = await AddAsync(colleague, "administrator");
        using var adminClient = await _factory.CreateAntiforgeryClientAsync(admin, _organization);
        using var colleagueClient = _factory.CreateHttpsClient(colleague, _organization);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(colleagueClient, StaffPath));

        Assert.Equal(HttpStatusCode.NoContent, await ChangeRolesAsync(adminClient, colleagueMembership, "staff"));

        var audit = Assert.Single(await ReadAuditAsync(colleagueMembership), a => a.Action == "Updated");
        Assert.Equal(admin, audit.ActorId);
        Assert.Equal("administrator", JsonNode.Parse(audit.BeforeJson!)!["RoleKeys"]!.AsArray().Single()!.GetValue<string>());
        Assert.Equal("staff", JsonNode.Parse(audit.AfterJson!)!["RoleKeys"]!.AsArray().Single()!.GetValue<string>());

        // Staff loses the directory (colleagues' emails) and management; keeps non-sensitive reads.
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(colleagueClient, StaffPath));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(colleagueClient, SpeciesPath));
        using var demoted = await _factory.CreateAntiforgeryClientAsync(colleague, _organization);
        Assert.Equal(HttpStatusCode.Forbidden, await ChangeRolesAsync(demoted, colleagueMembership, "administrator"));
    }

    [Fact]
    public async Task Last_active_administrator_can_be_neither_demoted_nor_suspended()
    {
        var admin = Guid.CreateVersion7();
        var membership = await AddAsync(admin, "administrator");
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, _organization);

        Assert.Equal(HttpStatusCode.Conflict, await ChangeRolesAsync(client, membership, "staff"));
        Assert.Equal(HttpStatusCode.Conflict, await PostAsync(client, membership, "suspend"));
        Assert.Equal(["administrator"], await RoleKeysAsync(membership));
    }

    [Fact]
    public async Task Of_two_administrators_only_one_can_be_demoted()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var firstMembership = await AddAsync(first, "administrator");
        var secondMembership = await AddAsync(second, "administrator");
        using var firstClient = await _factory.CreateAntiforgeryClientAsync(first, _organization);

        Assert.Equal(HttpStatusCode.NoContent, await ChangeRolesAsync(firstClient, secondMembership, "staff"));
        Assert.Equal(HttpStatusCode.Conflict, await ChangeRolesAsync(firstClient, firstMembership, "staff"));
    }

    [Fact]
    public async Task Membership_of_another_organization_is_not_found_and_unchanged()
    {
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "administrator");
        var otherOrganization = Guid.CreateVersion7();
        var otherMembership = await TestMemberships.AddAsync(postgres.AppConnectionString, otherOrganization, Guid.CreateVersion7(), roles: ["staff"]);
        using var client = await _factory.CreateAntiforgeryClientAsync(admin, _organization);

        Assert.Equal(HttpStatusCode.NotFound, await ChangeRolesAsync(client, otherMembership, "administrator"));
        Assert.Equal(HttpStatusCode.NotFound, await PostAsync(client, otherMembership, "suspend"));
        Assert.Equal(["staff"], await RoleKeysAsync(otherMembership, otherOrganization));
    }

    [Fact]
    public async Task Staff_cannot_manage_and_invalid_roles_are_rejected()
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "staff");
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "administrator");
        var target = await AddAsync(Guid.CreateVersion7(), "read_only");
        using var staffClient = await _factory.CreateAntiforgeryClientAsync(staff, _organization);
        using var adminClient = await _factory.CreateAntiforgeryClientAsync(admin, _organization);

        Assert.Equal(HttpStatusCode.Forbidden, await ChangeRolesAsync(staffClient, target, "staff"));
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(staffClient, target, "suspend"));
        Assert.Equal(HttpStatusCode.BadRequest, await ChangeRolesAsync(adminClient, target, "owner"));
        Assert.Equal(HttpStatusCode.BadRequest, await ChangeRolesAsync(adminClient, target));
        Assert.Equal(["read_only"], await RoleKeysAsync(target));
    }

    [Fact]
    public async Task Suspension_blocks_access_until_reactivated()
    {
        var admin = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "administrator");
        var colleague = Guid.CreateVersion7();
        var colleagueMembership = await AddAsync(colleague, "staff");
        using var adminClient = await _factory.CreateAntiforgeryClientAsync(admin, _organization);
        using var colleagueClient = _factory.CreateHttpsClient(colleague, _organization);

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(adminClient, colleagueMembership, "suspend"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(colleagueClient, SpeciesPath));

        Assert.Equal(HttpStatusCode.NoContent, await PostAsync(adminClient, colleagueMembership, "reactivate"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(colleagueClient, SpeciesPath));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> ChangeRolesAsync(HttpClient client, Guid membershipId, params string[] roles)
    {
        using var response = await client.PutAsJsonAsync(
            new Uri($"{StaffPath}/{membershipId}/roles", UriKind.Relative), new { roles }, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, Guid membershipId, string action)
    {
        using var response = await client.PostAsync(
            new Uri($"{StaffPath}/{membershipId}/{action}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private Task<Guid> AddAsync(Guid userId, params string[] roles) =>
        TestMemberships.AddAsync(postgres.AppConnectionString, _organization, userId, roles: roles);

    private Task<List<string[]>> RoleKeysQueryAsync(Guid membershipId, Guid organization) =>
        InTenantScopeAsync(organization, services => services.GetRequiredService<ShelterDbContext>().Database
            .SqlQuery<string[]>($"""SELECT role_keys AS "Value" FROM platform.staff_membership WHERE id = {membershipId}""")
            .ToListAsync(TestContext.Current.CancellationToken));

    private async Task<string[]> RoleKeysAsync(Guid membershipId, Guid? organization = null) =>
        Assert.Single(await RoleKeysQueryAsync(membershipId, organization ?? _organization));

    private Task<List<AuditRow>> ReadAuditAsync(Guid membershipId) =>
        InTenantScopeAsync(_organization, services => services.GetRequiredService<ShelterDbContext>().Database
            .SqlQuery<AuditRow>(
                $"""
                SELECT action, actor_id, before_json::text AS before_json, after_json::text AS after_json
                FROM audit.audit_event
                WHERE entity_id = {membershipId.ToString("D")}
                """)
            .ToListAsync(TestContext.Current.CancellationToken));

    private async Task<T> InTenantScopeAsync<T>(Guid organization, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(organization);
        return await scope.ServiceProvider.GetRequiredService<UnitOfWork>()
            .ExecuteAsync(_ => work(scope.ServiceProvider), static _ => true, TestContext.Current.CancellationToken);
    }

    private sealed record StaffMember(Guid MembershipId, Guid UserId, string DisplayName, string Email, string Status, string[] Roles);

    private sealed record AuditRow(string Action, Guid? ActorId, string? BeforeJson, string? AfterJson);
}
