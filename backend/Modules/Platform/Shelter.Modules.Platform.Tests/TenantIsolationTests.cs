using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Tests;

/// <summary>M1-3: the standard isolation checks for every Platform tenant-owned entity.</summary>
public sealed class TenantIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public Task Tenant_setting_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => new TenantSetting("isolation", "fr-CA"), TestContext.Current.CancellationToken);

    // M3-1: the runtime role has no DELETE and no UPDATE on tenant_id here, so the harness's raw writes get 42501.
    // This keeps the harness honest for the append-only tables that follow (movements, timeline).
    [Fact]
    public Task Staff_membership_is_isolated_without_update_or_delete_grants() =>
        fixture.Tenants.AssertIsolatedAsync(
            () => new StaffMembership(Guid.CreateVersion7(), ["staff"], DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
}
