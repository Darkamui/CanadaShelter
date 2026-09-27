using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Tests;

/// <summary>M1-2: RLS with <c>SET LOCAL</c> enforces tenant isolation in the database, below EF.</summary>
public sealed class TenantRlsTests(PostgresFixture fixture)
{
    private const string InsufficientPrivilege = "42501";

    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();

    [Fact]
    public async Task Tenant_read_outside_a_transaction_is_rejected()
    {
        await using var db = fixture.CreateContext(_tenantA);

        await Assert.ThrowsAsync<TenantTransactionRequiredException>(
            () => db.Set<TenantSetting>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Raw_sql_in_a_unit_of_work_sees_only_the_current_tenant()
    {
        await SeedAsync(_tenantA, "rls-raw");
        await SeedAsync(_tenantB, "rls-raw");

        // No tenant predicate and no EF query filter: only RLS scopes this query.
        var tenants = await fixture.InTenantAsync(_tenantA, db => db.Database
            .SqlQuery<Guid>($"SELECT tenant_id AS \"Value\" FROM platform.tenant_setting WHERE key = 'rls-raw'")
            .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal([_tenantA], tenants);
    }

    [Fact]
    public async Task Raw_insert_for_another_tenant_violates_the_policy()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.InTenantAsync(_tenantA, db => db.Database
            .ExecuteSqlAsync(
                $"INSERT INTO platform.tenant_setting (id, tenant_id, key, value) VALUES ({Guid.CreateVersion7()}, {_tenantB}, 'rls-insert', 'x')",
                TestContext.Current.CancellationToken)));

        Assert.Equal(InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Connection_without_a_tenant_sees_no_rows()
    {
        await SeedAsync(_tenantA, "rls-none");

        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM platform.tenant_setting", connection);

        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Runtime_role_cannot_write_organizations()
    {
        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO platform.organization (id, name, slug, status, created_at) VALUES (gen_random_uuid(), 'x', 'rls-denied', 'Active', now())",
            connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Provisioner_creates_an_organization_through_the_platform_admin_role()
    {
        var slug = $"org-{Guid.CreateVersion7():N}";

        var id = await fixture.CreateProvisioner().ProvisionAsync("Refuge test", slug, TestContext.Current.CancellationToken);

        // Organizations are global: the runtime role reads them without a tenant.
        await using var db = fixture.CreateContext(tenantId: null);
        var stored = await db.Set<Organization>().SingleAsync(o => o.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(slug, stored.Slug);
    }

    [Fact]
    public async Task Platform_admin_reads_across_tenants()
    {
        await SeedAsync(_tenantA, "rls-admin");
        await SeedAsync(_tenantB, "rls-admin");

        await using var admin = fixture.CreatePlatformAdminFactory().CreateDbContext();
        var tenants = await admin.Set<TenantSetting>()
            .IgnoreQueryFilters([ShelterDbContext.TenantFilter])
            .Where(s => s.Key == "rls-admin" && (s.TenantId == _tenantA || s.TenantId == _tenantB))
            .Select(s => s.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, tenants.Count);
        Assert.Contains(_tenantA, tenants);
        Assert.Contains(_tenantB, tenants);
    }

    [Fact]
    public async Task Platform_admin_write_without_a_tenant_id_is_rejected()
    {
        await using var admin = fixture.CreatePlatformAdminFactory().CreateDbContext();
        admin.Add(new TenantSetting("rls-admin-empty", "x"));

        await Assert.ThrowsAsync<TenantIsolationException>(() => admin.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Platform_admin_cannot_move_a_row_to_another_tenant()
    {
        await SeedAsync(_tenantA, "rls-admin-move");

        await using var admin = fixture.CreatePlatformAdminFactory().CreateDbContext();
        var setting = await admin.Set<TenantSetting>()
            .IgnoreQueryFilters([ShelterDbContext.TenantFilter])
            .SingleAsync(s => s.TenantId == _tenantA && s.Key == "rls-admin-move", TestContext.Current.CancellationToken);
        admin.Entry(setting).Property(s => s.TenantId).CurrentValue = _tenantB;

        await Assert.ThrowsAsync<TenantIsolationException>(() => admin.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Every_table_with_a_tenant_id_has_forced_rls()
    {
        await using var connection = await fixture.OpenMigratorConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT c.oid::regclass::text, c.relrowsecurity AND c.relforcerowsecurity
            FROM pg_class c
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
            WHERE c.relkind IN ('r', 'p')
              AND c.relnamespace NOT IN ('pg_catalog'::regnamespace, 'information_schema'::regnamespace)
            """,
            connection);

        var tables = new Dictionary<string, bool>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                tables[reader.GetString(0)] = reader.GetBoolean(1);
            }
        }

        Assert.Contains("platform.tenant_setting", tables.Keys);
        Assert.Empty(tables.Where(t => !t.Value).Select(t => t.Key));
    }

    private async Task SeedAsync(Guid tenantId, string key)
    {
        await using var db = fixture.CreateContext(tenantId);
        db.Add(new TenantSetting(key, "fr-CA"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
