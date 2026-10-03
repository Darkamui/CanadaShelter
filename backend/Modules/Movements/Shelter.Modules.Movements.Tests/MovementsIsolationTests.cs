using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shelter.BuildingBlocks.Localization;
using Shelter.Modules.Movements.Domain;
using Shelter.Testing;

namespace Shelter.Modules.Movements.Tests;

/// <summary>
/// M3-5: movements and the intake reason and outcome type overrides are tenant-isolated, and the movement ledger is
/// append-only for the runtime role.
/// </summary>
public sealed class MovementsIsolationTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public Task Movement_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => NewIntake(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Void_cannot_reference_another_tenants_movement()
    {
        var ct = TestContext.Current.CancellationToken;
        var intakeInA = await fixture.Tenants.SeedAsync(TenantHarness.NewTenantId(), NewIntake(), ct);
        var voidInB = Movement.Void(intakeInA, "Erreur de saisie", Now, Guid.CreateVersion7());

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Tenants.SeedAsync(TenantHarness.NewTenantId(), voidInB, ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public Task Intake_reason_override_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            () => new IntakeReasonOverride("found_cat", new LocalizedText("Chat trouvé", "Found cat"), 40, isHidden: false, SacCategories.Intake.Stray),
            TestContext.Current.CancellationToken);

    [Fact]
    public Task Outcome_type_override_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(
            () => new OutcomeTypeOverride("foster_adoption", new LocalizedText("Adoption en famille d'accueil", "Foster adoption"), 40, isHidden: false, SacCategories.Outcome.Adoption),
            TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("UPDATE movements.movement SET notes = 'x' WHERE tenant_id = @tenant")]
    [InlineData("DELETE FROM movements.movement WHERE tenant_id = @tenant")]
    public async Task Runtime_role_cannot_update_or_delete_a_movement(string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = TenantHarness.NewTenantId();
        await fixture.Tenants.SeedAsync(tenant, NewIntake(), ct);

        await using var connection = new NpgsqlConnection(fixture.AppConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection) { Parameters = { new("tenant", tenant) } };
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    private static Movement NewIntake() =>
        Movement.Intake(Guid.CreateVersion7(), "stray", Guid.CreateVersion7(), personId: null, notes: null, Now, Now, Guid.CreateVersion7());
}
