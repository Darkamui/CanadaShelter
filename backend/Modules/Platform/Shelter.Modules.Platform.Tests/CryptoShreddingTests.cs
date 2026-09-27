using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Testing.Auditing;

namespace Shelter.Modules.Platform.Tests;

/// <summary>
/// M1-6: personal values in audit payloads are encrypted with the subject's data key; an authorized reader decrypts
/// them; shredding the subject destroys the key, leaving the audit rows (actor, action, field names, timestamps)
/// with unrecoverable personal values.
/// </summary>
public sealed class CryptoShreddingTests(PostgresFixture fixture) : IAsyncDisposable
{
    private const string InsufficientPrivilege = "42501";
    private const string SamplePersonType = $"{SamplePerson.Schema}.{SamplePerson.Table}";

    private readonly ServiceProvider _services = fixture.CreateAuditServices();
    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();

    [Fact]
    public async Task Personal_values_are_stored_encrypted_and_decrypted_for_a_reader()
    {
        var person = await CreateAndRenameAsync();

        var raw = await RawAuditAsync(_tenantA, person.Id);
        Assert.Equal(2, raw.Count);
        Assert.All(raw, row => Assert.Equal(person.Id, row.SubjectId));
        foreach (var json in raw.SelectMany(r => new[] { r.BeforeJson, r.AfterJson }).OfType<string>())
        {
            Assert.DoesNotContain("example.ca", json, StringComparison.Ordinal);
            Assert.NotNull(JsonNode.Parse(json)!["Email"]!["$enc"]);
        }

        var history = await HistoryAsync(_tenantA, SamplePersonType, person.Id);
        Assert.Equal("marie@example.ca", Text(history[0].After, "Email"));
        Assert.Equal("marie@example.ca", Text(history[1].Before, "Email"));
        Assert.Equal("marie.tremblay@example.ca", Text(history[1].After, "Email"));
        Assert.Equal("B-13", Text(history[1].After, "Code"));
        Assert.Equal("unclassified", Text(history[1].After!["Nickname"]!.AsObject(), "$redacted"));
    }

    [Fact]
    public async Task Shredding_keeps_audit_rows_but_makes_personal_values_unrecoverable()
    {
        var person = await CreateAndRenameAsync();
        var beforeShred = await HistoryAsync(_tenantA, SamplePersonType, person.Id);

        await ShredAsync(_tenantA, person.Id);

        var afterShred = await HistoryAsync(_tenantA, SamplePersonType, person.Id);
        Assert.Equal(beforeShred.Select(Shape), afterShred.Select(Shape));
        foreach (var payload in afterShred.SelectMany(e => new[] { e.Before, e.After }).OfType<JsonObject>())
        {
            Assert.Equal("shredded", Text(payload["Email"]!.AsObject(), "$unrecoverable"));
            Assert.StartsWith("B-1", Text(payload, "Code"), StringComparison.Ordinal);
        }

        var shred = Assert.Single(await HistoryAsync(_tenantA, "audit.person_data_key", person.Id));
        Assert.Equal("Shredded", shred.Action);
        Assert.Equal(person.Id, shred.SubjectId);
        Assert.Equal("corr-shred-1", shred.CorrelationId);

        var key = Assert.Single(await RawKeysAsync(_tenantA, person.Id));
        Assert.Null(key.WrappedKey);
        Assert.NotNull(key.ShreddedAt);
    }

    [Fact]
    public async Task Shredded_subject_never_gets_a_new_key()
    {
        var person = await CreateAndRenameAsync();
        await ShredAsync(_tenantA, person.Id);

        await InScopeAsync(_tenantA, async services =>
        {
            var db = services.GetRequiredService<ShelterDbContext>();
            var tracked = await db.Set<SamplePerson>().SingleAsync(p => p.Id == person.Id, TestContext.Current.CancellationToken);
            tracked.Change("again@example.ca", "Mimi3", "B-14");
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var last = (await HistoryAsync(_tenantA, SamplePersonType, person.Id))[^1];
        Assert.Equal("shredded", Text(last.After!["Email"]!.AsObject(), "$redacted"));
        Assert.Equal("B-14", Text(last.After, "Code"));

        var key = Assert.Single(await RawKeysAsync(_tenantA, person.Id));
        Assert.Null(key.WrappedKey);
    }

    [Fact]
    public async Task Data_keys_are_isolated_by_tenant()
    {
        var person = await CreateAndRenameAsync();

        Assert.Single(await RawKeysAsync(_tenantA, person.Id));
        Assert.Empty(await RawKeysAsync(_tenantB, person.Id));

        // Raw connection with no tenant: RLS returns nothing.
        await using var connection = await fixture.OpenAppConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM audit.person_data_key", connection);
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Runtime_role_cannot_delete_keys_or_move_them_to_another_subject()
    {
        var person = await CreateAndRenameAsync();
        var id = person.Id;

        var delete = await Assert.ThrowsAsync<PostgresException>(() => fixture.InTenantAsync(_tenantA, db => db.Database.ExecuteSqlAsync(
            $"DELETE FROM audit.person_data_key WHERE subject_id = {id}", TestContext.Current.CancellationToken)));
        var move = await Assert.ThrowsAsync<PostgresException>(() => fixture.InTenantAsync(_tenantA, db => db.Database.ExecuteSqlAsync(
            $"UPDATE audit.person_data_key SET subject_id = {Guid.CreateVersion7()} WHERE subject_id = {id}", TestContext.Current.CancellationToken)));

        Assert.Equal(InsufficientPrivilege, delete.SqlState);
        Assert.Equal(InsufficientPrivilege, move.SqlState);
    }

    [Fact]
    public async Task Synchronous_save_of_personal_data_is_rejected()
    {
        await using var db = fixture.CreateAuditedContext(_tenantA, new AuditContext());
        db.Set<SamplePerson>().Add(new SamplePerson("sync@example.ca", "Sync", "S-1"));

        var error = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("SaveChangesAsync", error.Message, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private static object Shape(AuditEntry e) =>
        (e.Id, e.Action, e.ActorType, e.Source, e.TimestampUtc, Fields(e.Before), Fields(e.After));

    private static string Fields(JsonObject? payload) => payload is null ? string.Empty : string.Join(',', payload.Select(p => p.Key));

    private static string? Text(JsonObject? payload, string field) => payload?[field]?.GetValue<string>();

    private async Task<SamplePerson> CreateAndRenameAsync()
    {
        var person = new SamplePerson("marie@example.ca", "Mimi", "B-12");
        await InScopeAsync(_tenantA, services =>
        {
            var db = services.GetRequiredService<ShelterDbContext>();
            db.Set<SamplePerson>().Add(person);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await InScopeAsync(_tenantA, async services =>
        {
            var db = services.GetRequiredService<ShelterDbContext>();
            var tracked = await db.Set<SamplePerson>().SingleAsync(p => p.Id == person.Id, TestContext.Current.CancellationToken);
            tracked.Change("marie.tremblay@example.ca", "Mimi2", "B-13");
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return person;
    }

    private Task<bool> ShredAsync(Guid tenantId, Guid subjectId) =>
        InScopeAsync(tenantId, async services =>
        {
            await services.GetRequiredService<ISubjectShredder>().ShredSubjectAsync(subjectId, TestContext.Current.CancellationToken);
            return true;
        });

    private Task<IReadOnlyList<AuditEntry>> HistoryAsync(Guid tenantId, string entityType, Guid entityId) =>
        InScopeAsync(tenantId, services => services.GetRequiredService<IAuditReader>()
            .GetEntityHistoryAsync(entityType, entityId.ToString("D"), TestContext.Current.CancellationToken));

    private async Task<T> InScopeAsync<T>(Guid tenantId, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        scope.ServiceProvider.GetRequiredService<AuditContext>().Set(AuditActorType.Anonymous, actorId: null, AuditSources.Api, "corr-shred-1");
        return await scope.ServiceProvider.GetRequiredService<UnitOfWork>()
            .ExecuteAsync(_ => work(scope.ServiceProvider), static _ => true, TestContext.Current.CancellationToken);
    }

    private Task<List<RawAuditRow>> RawAuditAsync(Guid tenantId, Guid entityId) =>
        fixture.InTenantAsync(tenantId, db => db.Database
            .SqlQuery<RawAuditRow>(
                $"""
                SELECT subject_id, before_json::text AS before_json, after_json::text AS after_json
                FROM audit.audit_event
                WHERE entity_id = {entityId.ToString("D")}
                ORDER BY timestamp_utc, id
                """)
            .ToListAsync(TestContext.Current.CancellationToken));

    private Task<List<RawKeyRow>> RawKeysAsync(Guid tenantId, Guid subjectId) =>
        fixture.InTenantAsync(tenantId, db => db.Database
            .SqlQuery<RawKeyRow>($"SELECT wrapped_key, shredded_at FROM audit.person_data_key WHERE subject_id = {subjectId}")
            .ToListAsync(TestContext.Current.CancellationToken));

    private sealed record RawAuditRow(Guid? SubjectId, string? BeforeJson, string? AfterJson);

    private sealed record RawKeyRow(byte[]? WrappedKey, DateTimeOffset? ShreddedAt);
}
