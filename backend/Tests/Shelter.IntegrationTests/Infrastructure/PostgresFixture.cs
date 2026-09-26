using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Shelter.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container per test run, initialised with the same roles script as local compose
/// (<c>infrastructure/docker/postgres/init/01-roles.sh</c>). Tests connect as the runtime role <c>shelter_app</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image and digest as infrastructure/docker/compose.yml.
    private const string Image = "postgres:18.6@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722";
    private const string Database = "shelter";
    private const string AppPassword = "shelter_app_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithEnvironment("SHELTER_DB", Database)
        .WithEnvironment("SHELTER_MIGRATOR_PASSWORD", "shelter_migrator_test")
        .WithEnvironment("SHELTER_APP_PASSWORD", AppPassword)
        .WithResourceMapping(
            File.ReadAllBytes(RepositoryPaths.RolesInitScript),
            "/docker-entrypoint-initdb.d/01-roles.sh",
            Convert.ToUInt32("755", 8)) // executable, so the entrypoint runs it instead of sourcing it
        .Build();

    /// <summary>Connection string for the runtime role (<c>shelter_app</c>).</summary>
    public string AppConnectionString => new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
    {
        Database = Database,
        Username = "shelter_app",
        Password = AppPassword,
    }.ConnectionString;

    /// <inheritdoc />
    public async ValueTask InitializeAsync() => await _container.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
