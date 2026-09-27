using Hangfire;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>Schedules <see cref="ITenantJob{TArgs}"/>s for the current tenant.</summary>
public interface ITenantJobScheduler
{
    /// <summary>
    /// Enqueues <typeparamref name="TJob"/> for the current scope's tenant. The tenant is never a parameter, so a
    /// request cannot schedule work for another tenant. Throws <see cref="TenantContextMissingException"/> without
    /// a tenant. Returns the Hangfire job ID.
    /// </summary>
    string Enqueue<TJob, TArgs>(TArgs args)
        where TJob : ITenantJob<TArgs>;
}

/// <summary>Hangfire-backed <see cref="ITenantJobScheduler"/>. Scoped.</summary>
internal sealed class TenantJobScheduler(IBackgroundJobClient client, ITenantContext tenantContext, AuditContext auditContext)
    : ITenantJobScheduler
{
    public string Enqueue<TJob, TArgs>(TArgs args)
        where TJob : ITenantJob<TArgs>
    {
        var payload = new TenantJobPayload<TArgs>(tenantContext.RequireTenantId(), args, auditContext.CorrelationId);

        // Hangfire replaces CancellationToken.None with the job's own cancellation token when it runs.
        return client.Enqueue<TenantJobRunner<TJob, TArgs>>(runner => runner.RunAsync(payload, CancellationToken.None));
    }
}
