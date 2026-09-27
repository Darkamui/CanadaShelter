using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Features.Provisioning;
using Shelter.Modules.Platform.Persistence;
using Shelter.Testing;
using Shelter.Testing.Auditing;

[assembly: AssemblyFixture(typeof(Shelter.Modules.Platform.Tests.PostgresFixture))]

namespace Shelter.Modules.Platform.Tests;

/// <summary>One migrated PostgreSQL container per test run; contexts compose the Platform model only.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();

    /// <summary>Tenant-scoped runtime-role access over the Platform model, with the standard isolation checks.</summary>
    public TenantHarness Tenants => new(_database.AppConnectionString, new PlatformModelContributor());

    /// <summary>A runtime-role (<c>shelter_app</c>) context for <paramref name="tenantId"/>, or with no tenant.</summary>
    public ShelterDbContext CreateContext(Guid? tenantId) => Tenants.CreateContext(tenantId);

    /// <summary>Runs <paramref name="work"/> for <paramref name="tenantId"/> inside one committed unit of work.</summary>
    public Task<T> InTenantAsync<T>(Guid? tenantId, Func<ShelterDbContext, Task<T>> work) => Tenants.InTenantAsync(tenantId, work);

    /// <summary>
    /// A runtime-role context for <paramref name="tenantId"/> over the Platform model plus
    /// <see cref="SamplePerson"/>, stamping <paramref name="auditContext"/> on its audit events.
    /// </summary>
    public ShelterDbContext CreateAuditedContext(Guid tenantId, AuditContext auditContext) =>
        TestDbContexts.Create(_database.AppConnectionString, tenantId, auditContext, new PlatformModelContributor(), new SamplePersonModelContributor());

    /// <summary>
    /// Persistence and auditing services, as the Host registers them, over the Platform model plus
    /// <see cref="SamplePerson"/>. Dispose after use.
    /// </summary>
    public ServiceProvider CreateAuditServices() =>
        TestServices.Create(_database.AppConnectionString, new PlatformModelContributor(), new SamplePersonModelContributor());

    /// <summary>A platform-admin (<c>shelter_platform_admin</c>) context factory over the Platform model.</summary>
    public PlatformAdminDbContextFactory CreatePlatformAdminFactory() =>
        TestDbContexts.CreatePlatformAdminFactory(_database.PlatformAdminConnectionString, new PlatformModelContributor());

    /// <summary>The provisioner as the module registers it.</summary>
    internal OrganizationProvisioner CreateProvisioner() => new(CreatePlatformAdminFactory(), TimeProvider.System);

    /// <summary>A raw, open connection as the runtime role, bypassing EF (and so the tenant transaction).</summary>
    public async Task<NpgsqlConnection> OpenAppConnectionAsync()
    {
        var connection = new NpgsqlConnection(_database.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    /// <summary>A raw, open connection as the schema owner. Setup and catalog inspection only.</summary>
    public async Task<NpgsqlConnection> OpenMigratorConnectionAsync()
    {
        var connection = new NpgsqlConnection(_database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        await using var connection = new NpgsqlConnection(_database.MigratorConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(SamplePerson.CreateTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
