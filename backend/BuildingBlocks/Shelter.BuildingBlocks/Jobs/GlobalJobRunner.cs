using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>
/// Hangfire's entry point for every global job. Like <see cref="TenantJobRunner{TJob, TArgs}"/>: a new DI scope per
/// run, the audit source set to the job, one unit of work. It never sets a tenant. Public because Hangfire invokes it.
/// </summary>
public sealed class GlobalJobRunner<TJob, TArgs>(IServiceScopeFactory scopeFactory)
    where TJob : IGlobalJob<TArgs>
{
    /// <summary>Runs <typeparamref name="TJob"/>.</summary>
    public async Task RunAsync(GlobalJobPayload<TArgs> payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<AuditContext>()
            .Set(AuditActorType.System, actorId: null, AuditSources.Job(typeof(TJob).Name), payload.CorrelationId);

        var job = services.GetRequiredService<TJob>();
        await services.GetRequiredService<UnitOfWork>()
            .ExecuteAsync(ct => job.ExecuteAsync(payload.Args, ct), cancellationToken);
    }
}
