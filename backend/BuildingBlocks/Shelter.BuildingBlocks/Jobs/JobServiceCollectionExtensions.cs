using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>Registers the job schedulers, runners and jobs. Hangfire storage and server are the Host's.</summary>
public static class JobServiceCollectionExtensions
{
    /// <summary>Adds the tenant and global schedulers and runners.</summary>
    public static IServiceCollection AddShelterJobs(this IServiceCollection services)
    {
        services.TryAddScoped<ITenantJobScheduler, TenantJobScheduler>();
        services.TryAddTransient(typeof(TenantJobRunner<,>));
        services.TryAddScoped<IGlobalJobScheduler, GlobalJobScheduler>();
        services.TryAddTransient(typeof(GlobalJobRunner<,>));
        return services;
    }

    /// <summary>Registers <typeparamref name="TJob"/>, resolved by the runner in each job's own scope.</summary>
    public static IServiceCollection AddTenantJob<TJob, TArgs>(this IServiceCollection services)
        where TJob : class, ITenantJob<TArgs>
    {
        services.TryAddScoped<TJob>();
        return services;
    }

    /// <summary>Registers <typeparamref name="TJob"/>, resolved by the runner in each job's own scope.</summary>
    public static IServiceCollection AddGlobalJob<TJob, TArgs>(this IServiceCollection services)
        where TJob : class, IGlobalJob<TArgs>
    {
        services.TryAddScoped<TJob>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TJob"/> and runs it on <paramref name="cron"/> (UTC) with <paramref name="args"/>.
    /// The schedule reaches Hangfire when a host with a job server starts.
    /// </summary>
    public static IServiceCollection AddRecurringGlobalJob<TJob, TArgs>(this IServiceCollection services, string id, string cron, TArgs args)
        where TJob : class, IGlobalJob<TArgs>
    {
        services.AddGlobalJob<TJob, TArgs>();
        var payload = new GlobalJobPayload<TArgs>(args);
        services.AddSingleton(new RecurringGlobalJob(
            id,
            manager => manager.AddOrUpdate<GlobalJobRunner<TJob, TArgs>>(id, runner => runner.RunAsync(payload, CancellationToken.None), cron)));
        return services;
    }
}
