using System.Reflection;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Shelter.BuildingBlocks.Jobs;
using Shelter.BuildingBlocks.Persistence;

namespace Shelter.Host.Composition;

/// <summary>
/// Hangfire on PostgreSQL (ADR 0010), in its own <c>hangfire</c> schema. The schema is created by a migration;
/// Hangfire installs and upgrades its own tables in it as the runtime role. Its tables hold job arguments (IDs
/// only, never personal data) and are not tenant-owned.
/// </summary>
internal static class Jobs
{
    /// <summary>Hangfire's schema; created by the <c>AddHangfireSchema</c> migration.</summary>
    public const string Schema = "hangfire";

    /// <summary>When false, this instance only enqueues (integration tests, one-off tools). Default true.</summary>
    public const string ServerEnabledKey = "Jobs:ServerEnabled";

    /// <summary>How often idle workers poll the queue. Default 15 seconds.</summary>
    public const string QueuePollIntervalKey = "Jobs:QueuePollInterval";

    public static IServiceCollection AddShelterJobHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddShelterJobs();

        // Storage is built on first use, so a host that never touches jobs (OpenAPI export) needs no database.
        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(AppConnectionString(sp)),
                new PostgreSqlStorageOptions
                {
                    SchemaName = Schema,
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = configuration.GetValue(QueuePollIntervalKey, TimeSpan.FromSeconds(15)),
                }));

        // Build-time OpenAPI export resolves hosted services; it must not need a database.
        if (configuration.GetValue(ServerEnabledKey, true) && !IsBuildTimeDocumentGeneration())
        {
            services.AddHangfireServer();
        }

        return services;
    }

    /// <summary>
    /// The dashboard shows every tenant's jobs, so it is platform-admin only. Until staff authentication exists
    /// (TODO(M2): platform-admin policy) it is mapped only in Development and only for local requests; elsewhere
    /// <c>/hangfire</c> does not exist.
    /// </summary>
    public static WebApplication MapShelterJobDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
            });
        }

        return app;
    }

    private static bool IsBuildTimeDocumentGeneration() =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    private static string AppConnectionString(IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>().GetConnectionString(PersistenceServiceCollectionExtensions.AppConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{PersistenceServiceCollectionExtensions.AppConnectionName}' is not configured.");
}
