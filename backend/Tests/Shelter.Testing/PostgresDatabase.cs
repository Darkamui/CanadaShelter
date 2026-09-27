using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Testcontainers.PostgreSql;

namespace Shelter.Testing;

/// <summary>
/// One PostgreSQL container, initialised with the same roles script as local compose
/// (<c>infrastructure/docker/postgres/init/01-roles.sh</c>) and migrated as <c>shelter_migrator</c>.
/// Tests connect as the runtime role <c>shelter_app</c>.
/// </summary>
public sealed class PostgresDatabase : IAsyncDisposable
{
    // Same image and digest as infrastructure/docker/compose.yml.
    private const string Image = "postgres:18.6@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722";
    private const string Database = "shelter";
    private const string MigratorPassword = "shelter_migrator_test";
    private const string AppPassword = "shelter_app_test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithEnvironment("SHELTER_DB", Database)
        .WithEnvironment("SHELTER_MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("SHELTER_APP_PASSWORD", AppPassword)
        .WithResourceMapping(
            File.ReadAllBytes(RepositoryPaths.RolesInitScript),
            "/docker-entrypoint-initdb.d/01-roles.sh",
            Convert.ToUInt32("755", 8)) // executable, so the entrypoint runs it instead of sourcing it
        .Build();

    /// <summary>Connection string for the runtime role (<c>shelter_app</c>).</summary>
    public string AppConnectionString => ConnectionStringFor("shelter_app", AppPassword);

    /// <summary>Connection string for the schema owner (<c>shelter_migrator</c>). Test setup only.</summary>
    public string MigratorConnectionString => ConnectionStringFor("shelter_migrator", MigratorPassword);

    /// <summary>Starts the container and applies every migration.</summary>
    public async Task StartAsync()
    {
        await _container.StartAsync();

        // The migrations assembly carries the full model; this context needs none of it.
        var options = PersistenceServiceCollectionExtensions
            .Configure(new DbContextOptionsBuilder<ShelterDbContext>(), MigratorConnectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var context = new ShelterDbContext((DbContextOptions<ShelterDbContext>)options, new TenantContext(), []);
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private string ConnectionStringFor(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = Database,
            Username = username,
            Password = password,
        }.ConnectionString;
}
