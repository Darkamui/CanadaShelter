using Npgsql;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Features.Provisioning;
using Shelter.Modules.Platform.Persistence;
using Shelter.Testing;

[assembly: AssemblyFixture(typeof(Shelter.Modules.Platform.Tests.PostgresFixture))]

namespace Shelter.Modules.Platform.Tests;

/// <summary>One migrated PostgreSQL container per test run; contexts compose the Platform model only.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();

    /// <summary>A runtime-role (<c>shelter_app</c>) context for <paramref name="tenantId"/>, or with no tenant.</summary>
    public ShelterDbContext CreateContext(Guid? tenantId) =>
        TestDbContexts.Create(_database.AppConnectionString, tenantId, new PlatformModelContributor());

    /// <summary>Runs <paramref name="work"/> for <paramref name="tenantId"/> inside one committed unit of work.</summary>
    public async Task<T> InTenantAsync<T>(Guid? tenantId, Func<ShelterDbContext, Task<T>> work)
    {
        await using var db = CreateContext(tenantId);
        return await db.InUnitOfWorkAsync(() => work(db));
    }

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
    public async ValueTask InitializeAsync() => await _database.StartAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
