using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Tests;

/// <summary>M1-1: TenantId is stamped from TenantContext; cross-tenant and tenant-less access fails.</summary>
public sealed class TenantStampingTests(PostgresFixture fixture)
{
    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();

    [Fact]
    public async Task Insert_stamps_the_current_tenant()
    {
        var setting = new TenantSetting("locale", "fr-CA");
        await using (var db = fixture.CreateContext(_tenantA))
        {
            db.Add(setting);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = fixture.CreateContext(_tenantA);
        var stored = await reader.Set<TenantSetting>().SingleAsync(s => s.Id == setting.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_tenantA, stored.TenantId);
    }

    [Fact]
    public async Task Insert_for_another_tenant_is_rejected()
    {
        await using var db = fixture.CreateContext(_tenantA);
        var setting = new TenantSetting("locale", "fr-CA");
        db.Add(setting);
        db.Entry(setting).Property(s => s.TenantId).CurrentValue = _tenantB;

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Other_tenants_rows_are_invisible()
    {
        await SeedAsync(_tenantA, "locale");

        await using var db = fixture.CreateContext(_tenantB);
        Assert.Empty(await db.Set<TenantSetting>().Where(s => s.Key == "locale").ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_of_another_tenants_row_is_rejected()
    {
        var seeded = await SeedAsync(_tenantA, "locale");

        await using var db = fixture.CreateContext(_tenantB);
        // A caller that obtained another tenant's entity (e.g. from a cache) and hands it to this context.
        db.Attach(seeded);
        seeded.ChangeValue("en-CA");

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_of_another_tenants_row_is_rejected()
    {
        var seeded = await SeedAsync(_tenantA, "locale");

        await using var db = fixture.CreateContext(_tenantB);
        db.Remove(seeded);

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Moving_a_row_to_another_tenant_is_rejected()
    {
        var seeded = await SeedAsync(_tenantA, "locale");

        await using var db = fixture.CreateContext(_tenantA);
        var setting = await db.Set<TenantSetting>().SingleAsync(s => s.Id == seeded.Id, TestContext.Current.CancellationToken);
        db.Entry(setting).Property(s => s.TenantId).CurrentValue = _tenantB;

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_tenant_writes_throw()
    {
        await using var db = fixture.CreateContext(tenantId: null);
        db.Add(new TenantSetting("locale", "fr-CA"));

        await Assert.ThrowsAsync<TenantContextMissingException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_tenant_reads_return_nothing()
    {
        var seeded = await SeedAsync(_tenantA, "locale");

        await using var db = fixture.CreateContext(tenantId: null);
        Assert.Empty(await db.Set<TenantSetting>().Where(s => s.Id == seeded.Id).ToListAsync(TestContext.Current.CancellationToken));
    }

    private async Task<TenantSetting> SeedAsync(Guid tenantId, string key)
    {
        await using var db = fixture.CreateContext(tenantId);
        var setting = new TenantSetting(key, "fr-CA");
        db.Add(setting);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return setting;
    }
}
