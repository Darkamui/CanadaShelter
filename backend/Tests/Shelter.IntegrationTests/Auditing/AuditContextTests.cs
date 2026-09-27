using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Jobs;
using Shelter.BuildingBlocks.Modules;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Auditing;

/// <summary>
/// M1-5: audit events carry the actor, source and correlation of the request or job that caused them, and the
/// explicit <see cref="IAuditWriter"/> applies the same classification as tracked changes.
/// </summary>
[Collection(HangfireTests.Name)]
public sealed class AuditContextTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string CorrelationHeader = "X-Correlation-Id";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString, new AuditProbeModule())
    {
        EnvironmentName = "Development",
        ConfigureServices = services => services.AddTenantJob<AuditingJob, AuditingJobArgs>(),
    };

    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _entityId = Guid.CreateVersion7();

    [Fact]
    public async Task Request_stamps_source_api_anonymous_actor_and_its_correlation_id()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/test-audit/{_entityId:D}", UriKind.Relative));
        request.Headers.Add(TenantHeader, _tenant.ToString("D"));
        request.Headers.Add(CorrelationHeader, "corr-api-1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var row = Assert.Single(await ReadAuditAsync());
        Assert.Equal(AuditSources.Api, row.Source);
        Assert.Equal(nameof(AuditActorType.Anonymous), row.ActorType);
        Assert.Null(row.ActorId);
        Assert.Equal("corr-api-1", row.CorrelationId);
        Assert.Equal("Probed", row.Action);
    }

    [Fact]
    public async Task Job_stamps_its_source_system_actor_and_the_scheduling_correlation_id()
    {
        await _factory.Services.GetRequiredService<TenantJobRunner<AuditingJob, AuditingJobArgs>>().RunAsync(
            new TenantJobPayload<AuditingJobArgs>(_tenant, new AuditingJobArgs(_entityId), "corr-job-1"),
            TestContext.Current.CancellationToken);

        var row = Assert.Single(await ReadAuditAsync());
        Assert.Equal(AuditSources.Job(nameof(AuditingJob)), row.Source);
        Assert.Equal(nameof(AuditActorType.System), row.ActorType);
        Assert.Equal("corr-job-1", row.CorrelationId);
    }

    [Fact]
    public async Task Scheduled_job_carries_the_scheduling_correlation_id()
    {
        string jobId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
            scope.ServiceProvider.GetRequiredService<AuditContext>()
                .Set(AuditActorType.Anonymous, actorId: null, AuditSources.Api, "corr-schedule-1");
            jobId = scope.ServiceProvider.GetRequiredService<ITenantJobScheduler>()
                .Enqueue<AuditingJob, AuditingJobArgs>(new AuditingJobArgs(_entityId));
        }

        var job = _factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi().JobDetails(jobId).Job;

        // Never processed here; delete it so a job server in another test does not pick it up.
        _factory.Services.GetRequiredService<IBackgroundJobClient>().Delete(jobId);

        var payload = Assert.IsType<TenantJobPayload<AuditingJobArgs>>(job.Args[0]);
        Assert.Equal("corr-schedule-1", payload.CorrelationId);
    }

    [Fact]
    public async Task Explicit_record_applies_classification_and_the_scope_context()
    {
        await InTenantScopeAsync(async services =>
        {
            services.GetRequiredService<AuditContext>().Set(AuditActorType.System, actorId: null, "import:test", "corr-explicit-1");
            await services.GetRequiredService<IAuditWriter>().WriteAsync(
                new AuditRecord("platform.tenant_setting", _entityId.ToString("D"), "BulkReset")
                {
                    Before = [AuditField.NonPersonal("Value", "old"), AuditField.Personal("ContactEmail", "x@example.ca")],
                    After = [AuditField.NonPersonal("Value", "reset")],
                    Metadata = new Dictionary<string, string> { ["batch"] = "b-1" },
                },
                TestContext.Current.CancellationToken);
            return true;
        });

        var row = Assert.Single(await ReadAuditAsync());
        Assert.Equal("BulkReset", row.Action);
        Assert.Equal("import:test", row.Source);
        Assert.Equal("corr-explicit-1", row.CorrelationId);
        Assert.Equal("""{"Value":"old","ContactEmail":{"$redacted":"personal"}}""", Normalize(row.BeforeJson));
        Assert.Equal("""{"Value":"reset"}""", Normalize(row.AfterJson));
        Assert.Equal("""{"batch":"b-1"}""", Normalize(row.Metadata));
    }

    [Fact]
    public async Task Explicit_record_without_a_tenant_is_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<TenantContextMissingException>(() => scope.ServiceProvider.GetRequiredService<IAuditWriter>()
            .WriteAsync(new AuditRecord("x.y", "1", "Probed"), TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private Task<List<AuditRow>> ReadAuditAsync() =>
        InTenantScopeAsync(services => services.GetRequiredService<ShelterDbContext>().Database
            .SqlQuery<AuditRow>(
                $"""
                SELECT action, actor_type, actor_id, source, correlation_id,
                       before_json::text AS before_json, after_json::text AS after_json, metadata::text AS metadata
                FROM audit.audit_event
                WHERE entity_id = {_entityId.ToString("D")}
                """)
            .ToListAsync(TestContext.Current.CancellationToken));

    private async Task<T> InTenantScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
        return await scope.ServiceProvider.GetRequiredService<UnitOfWork>()
            .ExecuteAsync(_ => work(scope.ServiceProvider), static _ => true, TestContext.Current.CancellationToken);
    }

    // jsonb drops spacing and orders keys by length, then bytes; compare as compact JSON in that order.
    private static string? Normalize(string? json) =>
        json is null ? null : JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private sealed record AuditRow(
        string Action,
        string ActorType,
        Guid? ActorId,
        string Source,
        string? CorrelationId,
        string? BeforeJson,
        string? AfterJson,
        string? Metadata);

    /// <summary>Job arguments: an ID only.</summary>
    public sealed record AuditingJobArgs(Guid EntityId);

    /// <summary>Writes one explicit audit record for its entity.</summary>
    public sealed class AuditingJob(IAuditWriter writer) : ITenantJob<AuditingJobArgs>
    {
        public Task ExecuteAsync(AuditingJobArgs args, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(args);
            return writer.WriteAsync(new AuditRecord("test.probe", args.EntityId.ToString("D"), "Probed"), cancellationToken);
        }
    }

    /// <summary>Test-only module writing one explicit audit record per request.</summary>
    private sealed class AuditProbeModule : IModule
    {
        public string RoutePrefix => "test-audit";

        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapPost("/{id:guid}", async (Guid id, IAuditWriter writer, CancellationToken ct) =>
            {
                await writer.WriteAsync(new AuditRecord("test.probe", id.ToString("D"), "Probed"), ct);
                return Results.NoContent();
            });
    }
}
