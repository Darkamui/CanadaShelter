using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>
/// Hangfire's entry point for every tenant job. Each run gets a new DI scope, so no tenant context, DbContext or
/// transaction leaks between jobs; the tenant is set before the job is resolved, and the job runs inside one
/// unit of work, so <c>SET LOCAL app.tenant_id</c> covers all of its data access (ADR 0004, ADR 0010).
/// A payload without a tenant fails before any data access. A closed generic type (not a generic method) so
/// Hangfire resolves the method from the stored job reliably.
/// </summary>
public sealed class TenantJobRunner<TJob, TArgs>(IServiceScopeFactory scopeFactory)
    where TJob : ITenantJob<TArgs>
{
    /// <summary>Runs <typeparamref name="TJob"/> for the payload's tenant. Public because Hangfire invokes it.</summary>
    public async Task RunAsync(TenantJobPayload<TArgs> payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.TenantId == Guid.Empty)
        {
            throw new TenantContextMissingException("A tenant job has no tenant ID; it will not run.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TenantContext>().Set(payload.TenantId);

        var job = services.GetRequiredService<TJob>();
        await services.GetRequiredService<UnitOfWork>()
            .ExecuteAsync(ct => job.ExecuteAsync(payload.Args, ct), cancellationToken);
    }
}
