using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>Registers the tenant job scheduler, runner and jobs. Hangfire storage and server are the Host's.</summary>
public static class JobServiceCollectionExtensions
{
    /// <summary>Adds <see cref="ITenantJobScheduler"/> and <see cref="TenantJobRunner{TJob, TArgs}"/>.</summary>
    public static IServiceCollection AddShelterJobs(this IServiceCollection services)
    {
        services.TryAddScoped<ITenantJobScheduler, TenantJobScheduler>();
        services.TryAddTransient(typeof(TenantJobRunner<,>));
        return services;
    }

    /// <summary>Registers <typeparamref name="TJob"/>, resolved by the runner in each job's own scope.</summary>
    public static IServiceCollection AddTenantJob<TJob, TArgs>(this IServiceCollection services)
        where TJob : class, ITenantJob<TArgs>
    {
        services.TryAddScoped<TJob>();
        return services;
    }
}
