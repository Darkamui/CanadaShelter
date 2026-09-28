using Hangfire;
using Shelter.BuildingBlocks.Auditing;

namespace Shelter.BuildingBlocks.Jobs;

/// <summary>Schedules <see cref="IGlobalJob{TArgs}"/>s.</summary>
public interface IGlobalJobScheduler
{
    /// <summary>Enqueues <typeparamref name="TJob"/>. Returns the Hangfire job ID.</summary>
    string Enqueue<TJob, TArgs>(TArgs args)
        where TJob : IGlobalJob<TArgs>;
}

/// <summary>Hangfire-backed <see cref="IGlobalJobScheduler"/>. Scoped.</summary>
internal sealed class GlobalJobScheduler(IBackgroundJobClient client, AuditContext auditContext) : IGlobalJobScheduler
{
    public string Enqueue<TJob, TArgs>(TArgs args)
        where TJob : IGlobalJob<TArgs>
    {
        var payload = new GlobalJobPayload<TArgs>(args, auditContext.CorrelationId);

        // Hangfire replaces CancellationToken.None with the job's own cancellation token when it runs.
        return client.Enqueue<GlobalJobRunner<TJob, TArgs>>(runner => runner.RunAsync(payload, CancellationToken.None));
    }
}

/// <summary>
/// A global job on a schedule, registered with Hangfire when the host runs a job server. Registered through
/// <see cref="JobServiceCollectionExtensions.AddRecurringGlobalJob{TJob, TArgs}"/>.
/// </summary>
public sealed class RecurringGlobalJob
{
    private readonly Action<IRecurringJobManager> _register;

    internal RecurringGlobalJob(string id, Action<IRecurringJobManager> register)
    {
        Id = id;
        _register = register;
    }

    /// <summary>Hangfire's recurring job ID.</summary>
    public string Id { get; }

    /// <summary>Adds or updates the schedule in Hangfire's storage.</summary>
    public void Register(IRecurringJobManager manager) => _register(manager);
}
