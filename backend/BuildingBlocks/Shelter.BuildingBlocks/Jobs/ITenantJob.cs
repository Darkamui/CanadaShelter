namespace Shelter.BuildingBlocks.Jobs;

/// <summary>
/// A background job that works on one tenant's data (ADR 0010). Scheduled with <see cref="ITenantJobScheduler"/>
/// and run by <see cref="TenantJobRunner{TJob, TArgs}"/> in its own DI scope, with the tenant context and <c>SET LOCAL</c>
/// restored and inside one unit of work. Register with
/// <see cref="JobServiceCollectionExtensions.AddTenantJob{TJob, TArgs}"/>.
/// </summary>
/// <typeparam name="TArgs">
/// Job arguments: a small JSON-serializable record of IDs and codes only. Hangfire stores them in plain tables
/// without RLS or encryption, so they must never carry personal data (Law 25).
/// </typeparam>
public interface ITenantJob<in TArgs>
{
    /// <summary>Runs the job. The tenant context is already set and a transaction is open.</summary>
    Task ExecuteAsync(TArgs args, CancellationToken cancellationToken);
}

/// <summary>
/// What Hangfire stores for a tenant job: the tenant, taken from the scheduling scope's tenant context, the
/// job's arguments, and the scheduling scope's correlation ID (for the audit trail). Public only because
/// Hangfire serializes it.
/// </summary>
public sealed record TenantJobPayload<TArgs>(Guid TenantId, TArgs Args, string? CorrelationId = null);
