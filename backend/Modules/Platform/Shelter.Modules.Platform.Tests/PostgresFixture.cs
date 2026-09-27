using Shelter.BuildingBlocks.Persistence;
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

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await _database.StartAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
