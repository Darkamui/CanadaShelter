using Shelter.Testing;

[assembly: AssemblyFixture(typeof(Shelter.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>One migrated PostgreSQL container per test run (<see cref="PostgresDatabase"/>).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgresDatabase Database { get; } = new();

    /// <summary>Connection string for the runtime role (<c>shelter_app</c>).</summary>
    public string AppConnectionString => Database.AppConnectionString;

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await Database.StartAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
