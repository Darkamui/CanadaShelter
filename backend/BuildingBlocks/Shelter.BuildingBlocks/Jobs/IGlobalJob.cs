namespace Shelter.BuildingBlocks.Jobs;

/// <summary>
/// A background job that works on no tenant's data: platform-wide tables such as accounts or global lookup tables
/// (ADR 0010). Scheduled with <see cref="IGlobalJobScheduler"/>, or on a schedule with
/// <see cref="JobServiceCollectionExtensions.AddRecurringGlobalJob{TJob, TArgs}"/>, and run by
/// <see cref="GlobalJobRunner{TJob, TArgs}"/> in its own DI scope, inside one unit of work, with no tenant context: RLS
/// hides every tenant-owned row. Work on tenant data is an <see cref="ITenantJob{TArgs}"/>.
/// </summary>
/// <typeparam name="TArgs">
/// Job arguments: IDs and codes only, never personal data (Hangfire stores them in plain tables, Law 25).
/// </typeparam>
public interface IGlobalJob<in TArgs>
{
    /// <summary>Runs the job. No tenant context is set; a transaction is open.</summary>
    Task ExecuteAsync(TArgs args, CancellationToken cancellationToken);
}

/// <summary>What Hangfire stores for a global job. Public only because Hangfire serializes it.</summary>
public sealed record GlobalJobPayload<TArgs>(TArgs Args, string? CorrelationId = null);

/// <summary>Arguments of a job that needs none.</summary>
public sealed record NoJobArgs
{
    /// <summary>The only value.</summary>
    public static NoJobArgs Value { get; } = new();
}
