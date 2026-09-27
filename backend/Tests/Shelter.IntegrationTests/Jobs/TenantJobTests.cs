using System.Collections.Concurrent;
using System.Net;
using Hangfire;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Jobs;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Jobs;

/// <summary>
/// M1-4: a tenant job runs in its own scope with the tenant context and <c>SET LOCAL</c> restored from its
/// payload, so it sees only its tenant's rows; a payload without a tenant fails before any data access.
/// The probe job uses raw SQL on purpose: no EF filter, only the database scopes what it sees.
/// </summary>
[Collection(HangfireTests.Name)]
public sealed class TenantJobTests(PostgresFixture postgres)
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(60);

    private const string Operator = AuthorizationPolicies.PlatformOperatorClaim;

    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();
    private readonly string _key = $"job-{Guid.CreateVersion7():N}";
    private readonly Observations _observations = new();

    [Fact]
    public async Task Job_for_tenant_a_cannot_read_tenant_bs_data()
    {
        await using var factory = CreateFactory(jobServer: false);
        await SeedAsync(factory, _tenantA);
        await SeedAsync(factory, _tenantB);

        await factory.Services.GetRequiredService<TenantJobRunner<ProbeJob, ProbeArgs>>()
            .RunAsync(new(_tenantA, new ProbeArgs(_key)), TestContext.Current.CancellationToken);

        var seen = await _observations.For(_key).Task;
        Assert.Equal(_tenantA.ToString("D"), seen.Tenant);
        Assert.Equal(1, seen.Count);
    }

    [Fact]
    public async Task Job_without_a_tenant_id_fails_before_running()
    {
        await using var factory = CreateFactory(jobServer: false);
        var runner = factory.Services.GetRequiredService<TenantJobRunner<ProbeJob, ProbeArgs>>();

        await Assert.ThrowsAsync<TenantContextMissingException>(() =>
            runner.RunAsync(new(Guid.Empty, new ProbeArgs(_key)), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TenantContextMissingException>(() =>
            runner.RunAsync(null!, TestContext.Current.CancellationToken));

        Assert.False(_observations.For(_key).Task.IsCompleted);
    }

    [Fact]
    public async Task Scheduling_without_a_tenant_is_rejected()
    {
        await using var factory = CreateFactory(jobServer: false);
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.Throws<TenantContextMissingException>(() =>
            scope.ServiceProvider.GetRequiredService<ITenantJobScheduler>().Enqueue<ProbeJob, ProbeArgs>(new ProbeArgs(_key)));
    }

    [Fact]
    public async Task Job_scheduled_by_tenant_a_runs_through_hangfire_as_tenant_a()
    {
        await using var factory = CreateFactory(jobServer: true);
        await SeedAsync(factory, _tenantA);
        await SeedAsync(factory, _tenantB);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenantA);
            scope.ServiceProvider.GetRequiredService<ITenantJobScheduler>().Enqueue<ProbeJob, ProbeArgs>(new ProbeArgs(_key));
        }

        var seen = await _observations.For(_key).Task.WaitAsync(JobTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(_tenantA.ToString("D"), seen.Tenant);
        Assert.Equal(1, seen.Count);
    }

    [Fact]
    public async Task Hangfire_job_without_a_tenant_id_fails()
    {
        await using var factory = CreateFactory(jobServer: true);
        var payload = new TenantJobPayload<ProbeArgs>(Guid.Empty, new ProbeArgs(_key));

        var jobId = factory.Services.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<TenantJobRunner<ProbeJob, ProbeArgs>>(runner => runner.RunAsync(payload, CancellationToken.None));

        var monitoring = factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        var deadline = DateTime.UtcNow + JobTimeout;
        while (monitoring.JobDetails(jobId).History.All(h => h.StateName != FailedState.StateName))
        {
            Assert.True(DateTime.UtcNow < deadline, "the job did not fail in time");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        var failed = monitoring.JobDetails(jobId).History.First(h => h.StateName == FailedState.StateName);
        Assert.Equal(typeof(TenantContextMissingException).FullName, failed.Data["ExceptionType"]);
        Assert.False(_observations.For(_key).Task.IsCompleted);
    }

    [Fact]
    public async Task Dashboard_requires_a_platform_operator_signed_in_with_mfa()
    {
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString);
        var user = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.Unauthorized, await DashboardStatusAsync(factory, null));
        Assert.Equal(HttpStatusCode.Forbidden, await DashboardStatusAsync(factory, user, ("amr", "mfa")));
        Assert.Equal(HttpStatusCode.Forbidden, await DashboardStatusAsync(factory, user, (Operator, "true"), ("amr", "pwd")));
        Assert.Equal(HttpStatusCode.OK, await DashboardStatusAsync(factory, user, (Operator, "true"), ("amr", "pwd"), ("amr", "mfa")));
    }

    private static async Task<HttpStatusCode> DashboardStatusAsync(ShelterApiFactory factory, Guid? user, params (string Type, string Value)[] claims)
    {
        using var client = factory.CreateHttpsClient();
        if (user is { } id)
        {
            client.SignInAs(id, organizationId: null, claims);
        }

        using var response = await client.GetAsync(new Uri("/hangfire", UriKind.Relative), TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private ShelterApiFactory CreateFactory(bool jobServer) =>
        new(postgres.AppConnectionString)
        {
            JobServerEnabled = jobServer,
            ConfigureServices = services =>
            {
                services.AddSingleton(_observations);
                services.AddTenantJob<ProbeJob, ProbeArgs>();
            },
        };

    private async Task SeedAsync(ShelterApiFactory factory, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ShelterDbContext>();
        await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            ct => db.Database.ExecuteSqlAsync(
                $"INSERT INTO platform.tenant_setting (id, tenant_id, key, value) VALUES (gen_random_uuid(), platform.current_tenant_id(), {_key}, 'x')",
                ct),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Job arguments: a key only (IDs and codes, never personal data).</summary>
    public sealed record ProbeArgs(string Key);

    /// <summary>What a probe job saw: the tenant set for its transaction and the rows RLS let it count.</summary>
    public sealed record Observation(string? Tenant, int Count);

    /// <summary>Probe results by key, shared between the test and the job's own scope.</summary>
    public sealed class Observations
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Observation>> _byKey = new();

        public TaskCompletionSource<Observation> For(string key) =>
            _byKey.GetOrAdd(key, _ => new TaskCompletionSource<Observation>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    /// <summary>Counts <c>platform.tenant_setting</c> rows for its key with raw SQL (no tenant predicate).</summary>
    public sealed class ProbeJob(ShelterDbContext db, Observations observations) : ITenantJob<ProbeArgs>
    {
        public async Task ExecuteAsync(ProbeArgs args, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(args);
            var seen = await db.Database
                .SqlQuery<Observation>(
                    $"""
                    SELECT current_setting('app.tenant_id', true) AS tenant,
                           (SELECT count(*)::int FROM platform.tenant_setting WHERE key = {args.Key}) AS count
                    """)
                .SingleAsync(cancellationToken);
            observations.For(args.Key).TrySetResult(seen);
        }
    }
}
