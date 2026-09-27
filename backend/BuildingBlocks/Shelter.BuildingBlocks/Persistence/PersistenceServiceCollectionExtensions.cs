using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>Registers the composed <see cref="ShelterDbContext"/> for the runtime role.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Runtime connection string name (role <c>shelter_app</c>).</summary>
    public const string AppConnectionName = "App";

    /// <summary>The single migrations assembly (ADR 0005).</summary>
    public const string MigrationsAssembly = "Shelter.Migrations";

    /// <summary>Schema of the EF migrations history table (ADR 0005).</summary>
    public const string MigrationsHistorySchema = "platform";

    /// <summary>EF migrations history table (ADR 0005).</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Which <see cref="IKeyProvider"/> wraps audit data keys: <c>Local</c> (default) or <c>AwsKms</c> (ADR 0016).</summary>
    public const string KeyProviderKey = "Audit:KeyProvider";

    /// <summary>Base64 256-bit master key for the <c>Local</c> key provider.</summary>
    public const string MasterKeyKey = "Audit:MasterKey";

    /// <summary>
    /// Adds <see cref="ShelterDbContext"/> on the <c>App</c> connection string, resolved lazily so a host that never
    /// touches the database (OpenAPI export) starts without one. Deliberately no <c>NpgsqlDataSource</c> in DI: a raw
    /// connection would bypass the tenant filter, stamping and (M1-2) the tenant transaction.
    /// </summary>
    public static IServiceCollection AddShelterPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ShelterDbContext>((sp, options) => Configure(options, AppConnectionString(sp)));
        services.AddScoped<UnitOfWork>();
        services.AddScoped<AuditContext>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<ISubjectShredder, SubjectShredder>();
        services.AddSingleton(sp => CreateKeyProvider(sp.GetRequiredService<IConfiguration>()));
        return services;
    }

    /// <summary>Applies the shared provider, naming, migrations and interceptor settings.</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(MigrationsAssembly)
                .MigrationsHistoryTable(MigrationsHistoryTable, MigrationsHistorySchema))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, ShelterModelCacheKeyFactory>()
            .AddInterceptors(
                TenantStampingInterceptor.Instance,
                AuditSaveChangesInterceptor.Instance,
                TenantTransactionInterceptor.Instance,
                TenantCommandGuardInterceptor.Instance);
    }

    /// <summary>
    /// The configured key provider. Throws when none is usable: personal audit values are never written under a
    /// missing or made-up key. The Host resolves it at startup so misconfiguration fails fast.
    /// </summary>
    public static IKeyProvider CreateKeyProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration[KeyProviderKey] switch
        {
            null or "" or "Local" => LocalMasterKeyProvider.FromBase64(configuration[MasterKeyKey] is { Length: > 0 } key
                ? key
                : throw new InvalidOperationException($"{MasterKeyKey} is not configured.")),
            "AwsKms" => new AwsKmsKeyProvider(),
            var other => throw new InvalidOperationException($"Unknown {KeyProviderKey} '{other}'."),
        };
    }

    private static string AppConnectionString(IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>().GetConnectionString(AppConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{AppConnectionName}' is not configured.");
}
