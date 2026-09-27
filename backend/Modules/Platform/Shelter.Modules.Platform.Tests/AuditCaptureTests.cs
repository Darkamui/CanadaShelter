using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;
using Shelter.Testing;
using Shelter.Testing.Auditing;

namespace Shelter.Modules.Platform.Tests;

/// <summary>
/// M1-5: every tracked change of a tenant-owned entity writes an append-only audit row in the same transaction,
/// with the actor, source and correlation of its scope; only non-personal values are stored in plain form.
/// Audit rows are read back with raw SQL as the runtime role, as an auditor would.
/// </summary>
public sealed class AuditCaptureTests(PostgresFixture fixture)
{
    private const string InsufficientPrivilege = "42501";

    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();
    private readonly AuditContext _audit = ApiRequest("corr-audit-1");

    [Fact]
    public async Task Update_records_before_and_after_of_changed_fields_with_actor_source_and_correlation()
    {
        var setting = await AddAsync(_tenantA, db => db.Set<TenantSetting>().Add(new TenantSetting($"audit-{Guid.CreateVersion7():N}", "old")));

        await SaveAsync(_tenantA, async db =>
        {
            var tracked = await db.Set<TenantSetting>().SingleAsync(s => s.Id == setting.Entity.Id, TestContext.Current.CancellationToken);
            tracked.ChangeValue("new");
        });

        var rows = await ReadAuditAsync(_tenantA, setting.Entity.Id);
        Assert.Equal([AuditActions.Created, AuditActions.Updated], rows.Select(r => r.Action));

        var updated = rows[1];
        Assert.Equal("platform.tenant_setting", updated.EntityType);
        Assert.Equal(nameof(AuditActorType.Anonymous), updated.ActorType);
        Assert.Null(updated.ActorId);
        Assert.Equal(AuditSources.Api, updated.Source);
        Assert.Equal("corr-audit-1", updated.CorrelationId);
        Assert.Equal("""{"Value":"old"}""", Normalize(updated.BeforeJson));
        Assert.Equal("""{"Value":"new"}""", Normalize(updated.AfterJson));
    }

    [Fact]
    public async Task Create_and_delete_record_every_field()
    {
        var key = $"audit-{Guid.CreateVersion7():N}";
        var setting = await AddAsync(_tenantA, db => db.Set<TenantSetting>().Add(new TenantSetting(key, "v")));
        await SaveAsync(_tenantA, async db =>
        {
            var tracked = await db.Set<TenantSetting>().SingleAsync(s => s.Id == setting.Entity.Id, TestContext.Current.CancellationToken);
            db.Set<TenantSetting>().Remove(tracked);
        });

        var rows = await ReadAuditAsync(_tenantA, setting.Entity.Id);
        Assert.Equal([AuditActions.Created, AuditActions.Deleted], rows.Select(r => r.Action));

        var expected = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["Id"] = setting.Entity.Id,
            ["Key"] = key,
            ["Value"] = "v",
            ["TenantId"] = _tenantA,
        });
        Assert.Null(rows[0].BeforeJson);
        Assert.Equal(expected, Normalize(rows[0].AfterJson));
        Assert.Equal(expected, Normalize(rows[1].BeforeJson));
        Assert.Null(rows[1].AfterJson);
    }

    [Fact]
    public async Task Personal_and_unclassified_values_are_never_stored_in_plain_form()
    {
        var person = await AddAsync(_tenantA, db => db.Set<SamplePerson>().Add(new SamplePerson("marie@example.ca", "Mimi", "B-12")));
        await SaveAsync(_tenantA, async db =>
            (await db.Set<SamplePerson>().SingleAsync(p => p.Id == person.Entity.Id, TestContext.Current.CancellationToken))
                .Change("marie.tremblay@example.ca", "Mimi2", "B-13"));

        var rows = await ReadAuditAsync(_tenantA, person.Entity.Id);
        Assert.Equal(2, rows.Count);

        // Personal: encrypted (M1-6). Unclassified: never written.
        var updated = rows[1];
        foreach (var (json, code) in new[] { (updated.BeforeJson, "B-12"), (updated.AfterJson, "B-13") })
        {
            var payload = JsonNode.Parse(json!)!.AsObject();
            Assert.Equal(code, payload["Code"]!.GetValue<string>());
            Assert.StartsWith("v1:", payload["Email"]!["$enc"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal("""{"$redacted":"unclassified"}""", payload["Nickname"]!.ToJsonString());
        }

        foreach (var json in rows.SelectMany(r => new[] { r.BeforeJson, r.AfterJson }))
        {
            Assert.DoesNotContain("example.ca", json ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("Mimi", json ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Change_rolled_back_leaves_no_audit_row()
    {
        var setting = new TenantSetting($"audit-{Guid.CreateVersion7():N}", "v");

        await using (var db = fixture.CreateAuditedContext(_tenantA, _audit))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UnitOfWork(db).ExecuteAsync(
                async ct =>
                {
                    db.Set<TenantSetting>().Add(setting);
                    await db.SaveChangesAsync(ct);
                    throw new InvalidOperationException("roll back");
                },
                TestContext.Current.CancellationToken));
        }

        Assert.Empty(await ReadAuditAsync(_tenantA, setting.Id));
    }

    [Fact]
    public async Task Runtime_role_cannot_update_or_delete_audit_rows()
    {
        var setting = await AddAsync(_tenantA, db => db.Set<TenantSetting>().Add(new TenantSetting($"audit-{Guid.CreateVersion7():N}", "v")));
        var id = setting.Entity.Id.ToString("D");

        var update = await Assert.ThrowsAsync<PostgresException>(() => fixture.InTenantAsync(_tenantA, db => db.Database.ExecuteSqlAsync(
            $"UPDATE audit.audit_event SET action = 'Tampered' WHERE entity_id = {id}", TestContext.Current.CancellationToken)));
        var delete = await Assert.ThrowsAsync<PostgresException>(() => fixture.InTenantAsync(_tenantA, db => db.Database.ExecuteSqlAsync(
            $"DELETE FROM audit.audit_event WHERE entity_id = {id}", TestContext.Current.CancellationToken)));

        Assert.Equal(InsufficientPrivilege, update.SqlState);
        Assert.Equal(InsufficientPrivilege, delete.SqlState);
        Assert.Single(await ReadAuditAsync(_tenantA, setting.Entity.Id));
    }

    [Fact]
    public async Task Audit_rows_are_isolated_by_tenant()
    {
        var setting = await AddAsync(_tenantA, db => db.Set<TenantSetting>().Add(new TenantSetting($"audit-{Guid.CreateVersion7():N}", "v")));

        Assert.Single(await ReadAuditAsync(_tenantA, setting.Entity.Id));
        Assert.Empty(await ReadAuditAsync(_tenantB, setting.Entity.Id));

        // Raw connection with no tenant: RLS returns nothing.
        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM audit.audit_event", connection);
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static AuditContext ApiRequest(string correlationId)
    {
        var audit = new AuditContext();
        audit.Set(AuditActorType.Anonymous, actorId: null, AuditSources.Api, correlationId);
        return audit;
    }

    private async Task<TEntry> AddAsync<TEntry>(Guid tenantId, Func<ShelterDbContext, TEntry> add)
    {
        await using var db = fixture.CreateAuditedContext(tenantId, _audit);
        return await db.InUnitOfWorkAsync(async () =>
        {
            var entry = add(db);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return entry;
        });
    }

    private async Task SaveAsync(Guid tenantId, Func<ShelterDbContext, Task> change)
    {
        await using var db = fixture.CreateAuditedContext(tenantId, _audit);
        await db.InUnitOfWorkAsync(async () =>
        {
            await change(db);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
    }

    private Task<List<AuditRow>> ReadAuditAsync(Guid tenantId, Guid entityId) =>
        fixture.InTenantAsync(tenantId, db => db.Database
            .SqlQuery<AuditRow>(
                $"""
                SELECT entity_type, action, actor_type, actor_id, source, correlation_id,
                       before_json::text AS before_json, after_json::text AS after_json, metadata::text AS metadata
                FROM audit.audit_event
                WHERE entity_id = {entityId.ToString("D")}
                ORDER BY timestamp_utc, id
                """)
            .ToListAsync(TestContext.Current.CancellationToken));

    // jsonb drops spacing and orders keys by length, then bytes; compare as compact JSON in that order.
    private static string? Normalize(string? json) =>
        json is null ? null : JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);

    private sealed record AuditRow(
        string EntityType,
        string Action,
        string ActorType,
        Guid? ActorId,
        string Source,
        string? CorrelationId,
        string? BeforeJson,
        string? AfterJson,
        string? Metadata);
}
