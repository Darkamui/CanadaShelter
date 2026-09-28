using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Jobs;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.Jobs;

/// <summary>
/// M2-8: global jobs run without a tenant (RLS hides every tenant row), and a host with a job server registers the modules' recurring jobs. The
/// hourly invitation token purge deletes expired tokens only.
/// </summary>
[Collection(HangfireTests.Name)]
public sealed class GlobalJobTests(PostgresFixture postgres)
{
    private const string PurgeJob = "platform:purge-expired-invitation-tokens";
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Recurring_purge_deletes_expired_invitation_tokens_only()
    {
        var expired = Guid.CreateVersion7();
        var pending = Guid.CreateVersion7();
        await InsertTokenAsync(expired, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertTokenAsync(pending, DateTimeOffset.UtcNow.AddDays(1));

        await using var factory = new ShelterApiFactory(postgres.AppConnectionString) { JobServerEnabled = true };
        _ = factory.Services; // Starts the host, which registers the recurring jobs.
        factory.Services.GetRequiredService<IRecurringJobManager>().Trigger(PurgeJob);

        var deadline = DateTime.UtcNow + JobTimeout;
        while (await TokenExistsAsync(expired))
        {
            Assert.True(DateTime.UtcNow < deadline, "the purge did not run in time");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.True(await TokenExistsAsync(pending));
    }

    [Fact]
    public async Task Global_job_sees_no_tenant_rows()
    {
        var key = $"global-{Guid.CreateVersion7():N}";
        var observations = new TenantJobTests.Observations();
        await using var factory = new ShelterApiFactory(postgres.AppConnectionString)
        {
            ConfigureServices = services =>
            {
                services.AddSingleton(observations);
                services.AddGlobalJob<GlobalProbeJob, TenantJobTests.ProbeArgs>();
            },
        };
        await SeedTenantSettingAsync(factory, Guid.CreateVersion7(), key);

        await factory.Services.GetRequiredService<GlobalJobRunner<GlobalProbeJob, TenantJobTests.ProbeArgs>>()
            .RunAsync(new(new TenantJobTests.ProbeArgs(key)), TestContext.Current.CancellationToken);

        var seen = await observations.For(key).Task;
        Assert.True(string.IsNullOrEmpty(seen.Tenant));
        Assert.Equal(0, seen.Count);
    }

    private static async Task SeedTenantSettingAsync(ShelterApiFactory factory, Guid tenantId, string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ShelterDbContext>();
        await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            ct => db.Database.ExecuteSqlAsync(
                $"INSERT INTO platform.tenant_setting (id, tenant_id, key, value) VALUES (gen_random_uuid(), platform.current_tenant_id(), {key}, 'x')",
                ct),
            TestContext.Current.CancellationToken);
    }

    private async Task InsertTokenAsync(Guid invitationId, DateTimeOffset expiresAt)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "INSERT INTO platform.invitation_token (token_hash, organization_id, invitation_id, expires_at) VALUES (@hash, @org, @id, @expires)",
            connection);
        command.Parameters.AddWithValue("hash", invitationId.ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());
        command.Parameters.AddWithValue("org", Guid.CreateVersion7());
        command.Parameters.AddWithValue("id", invitationId);
        command.Parameters.AddWithValue("expires", expiresAt);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> TokenExistsAsync(Guid invitationId)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM platform.invitation_token WHERE invitation_id = @id)", connection);
        command.Parameters.AddWithValue("id", invitationId);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Counts a tenant's <c>platform.tenant_setting</c> rows with raw SQL: RLS alone decides what it sees.</summary>
    public sealed class GlobalProbeJob(ShelterDbContext db, TenantJobTests.Observations observations) : IGlobalJob<TenantJobTests.ProbeArgs>
    {
        public async Task ExecuteAsync(TenantJobTests.ProbeArgs args, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(args);
            var seen = await db.Database
                .SqlQuery<TenantJobTests.Observation>(
                    $"""
                    SELECT current_setting('app.tenant_id', true) AS tenant,
                           (SELECT count(*)::int FROM platform.tenant_setting WHERE key = {args.Key}) AS count
                    """)
                .SingleAsync(cancellationToken);
            observations.For(args.Key).TrySetResult(seen);
        }
    }
}
