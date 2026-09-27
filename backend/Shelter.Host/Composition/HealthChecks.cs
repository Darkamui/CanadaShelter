using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Shelter.Host.Composition;

/// <summary><c>/health/live</c> (process up, no dependencies) and <c>/health/ready</c> (database reachable).</summary>
internal static class HealthChecks
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddShelterHealthChecks(this IServiceCollection services)
    {
        // Resolved lazily: /health/live and the OpenAPI export must work without a configured database.
        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("App")
                ?? throw new InvalidOperationException("Connection string 'App' is not configured.");
            return NpgsqlDataSource.Create(connectionString);
        });

        services.AddHealthChecks()
            .AddCheck<PostgresReadyCheck>("postgres", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IEndpointRouteBuilder MapShelterHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Infrastructure probes: anonymous by design; the body is only "Healthy"/"Unhealthy".
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .AllowAnonymous();

        return endpoints;
    }
}

/// <summary>Runs <c>SELECT 1</c> as the runtime role.</summary>
internal sealed partial class PostgresReadyCheck(IServiceProvider services, ILogger<PostgresReadyCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var dataSource = services.GetRequiredService<NpgsqlDataSource>();
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            // The exception type only: connection errors can echo host and user names.
            LogDatabaseUnreachable(logger, ex.GetType().Name);
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness check failed: database unreachable ({ExceptionType})")]
    private static partial void LogDatabaseUnreachable(ILogger logger, string exceptionType);
}
