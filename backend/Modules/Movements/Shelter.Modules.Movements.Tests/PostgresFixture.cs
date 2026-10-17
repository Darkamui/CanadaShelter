using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Movements.Persistence;
using Shelter.Testing;

[assembly: AssemblyFixture(typeof(Shelter.Modules.Movements.Tests.PostgresFixture))]

namespace Shelter.Modules.Movements.Tests;

/// <summary>One migrated PostgreSQL container per test run; contexts compose the Movements model only.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();

    /// <summary>Tenant-scoped runtime-role access over the Movements model, with the standard isolation checks.</summary>
    public TenantHarness Tenants => new(_database.AppConnectionString, new MovementsModelContributor());

    /// <summary>The runtime-role (<c>shelter_app</c>) connection string.</summary>
    public string AppConnectionString => _database.AppConnectionString;

    /// <summary>A runtime-role (<c>shelter_app</c>) context for <paramref name="tenantId"/>, or with no tenant.</summary>
    public ShelterDbContext CreateContext(Guid? tenantId) => Tenants.CreateContext(tenantId);

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await _database.StartAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
