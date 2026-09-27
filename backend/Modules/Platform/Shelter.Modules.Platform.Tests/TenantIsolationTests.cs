using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Tests;

/// <summary>M1-3: the standard isolation checks for every Platform tenant-owned entity.</summary>
public sealed class TenantIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public Task Tenant_setting_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => new TenantSetting("isolation", "fr-CA"), TestContext.Current.CancellationToken);
}
