using Shelter.Modules.Animals.Contracts;
using Shelter.Modules.Movements.Domain;
using Shelter.Modules.Movements.Features.Movements;

namespace Shelter.Modules.Movements.Tests;

/// <summary>M3-5: the custody summary a ledger projects, and which movement is the latest in effect.</summary>
public sealed class LedgerTests
{
    private static readonly Guid AnimalId = Guid.CreateVersion7();
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid Kennel = Guid.CreateVersion7();
    private static readonly Guid Room = Guid.CreateVersion7();
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Movement Intake(DateTimeOffset at, Guid? to = null) =>
        Movement.Intake(AnimalId, "stray", to ?? Kennel, null, null, at, at, User);

    private static Movement Relocation(DateTimeOffset at, Guid from, Guid to) =>
        Movement.Relocation(AnimalId, from, to, null, at, at, User);

    private static Movement Outcome(DateTimeOffset at, string code = "adoption") =>
        Movement.Outcome(AnimalId, code, Kennel, null, null, at, at, User);

    [Fact]
    public void Empty_ledger_projects_not_in_care()
    {
        var ledger = Ledger.Of([]);

        Assert.Null(ledger.Latest);
        Assert.Equal(AnimalCustodyState.NotInCare, ledger.Project());
    }

    [Fact]
    public void Intake_then_relocation_keeps_the_stay_and_moves_the_animal()
    {
        var intake = Intake(T0);
        var move = Relocation(T0.AddHours(1), Kennel, Room);

        var state = Ledger.Of([move, intake]).Project();

        Assert.Equal(new AnimalCustodyState(CustodyStatuses.InCare, Room, T0, intake.Id, null), state);
    }

    [Fact]
    public void Outcome_ends_the_stay_and_keeps_its_code_through_the_next_intake()
    {
        var first = Intake(T0);
        var outcome = Outcome(T0.AddDays(1), "transfer_out");
        var second = Intake(T0.AddDays(2), Room);

        var ledger = Ledger.Of([first, outcome]);
        Assert.Equal(new AnimalCustodyState(CustodyStatuses.Outcome, null, null, null, "transfer_out"), ledger.Project());

        var back = Ledger.Of([first, outcome, second]).Project();
        Assert.Equal(new AnimalCustodyState(CustodyStatuses.InCare, Room, T0.AddDays(2), second.Id, "transfer_out"), back);
    }

    [Fact]
    public void Movements_apply_in_occurred_order_not_list_order()
    {
        var intake = Intake(T0);
        var outcome = Outcome(T0.AddDays(1));

        var ledger = Ledger.Of([outcome, intake]);

        Assert.Equal([intake, outcome], ledger.InEffect);
        Assert.Same(outcome, ledger.Latest);
    }

    [Fact]
    public void Void_removes_the_voided_movement_and_is_not_in_effect_itself()
    {
        var intake = Intake(T0);
        var move = Relocation(T0.AddHours(1), Kennel, Room);
        var voidMove = Movement.Void(move, "Wrong kennel", T0.AddHours(2), User);

        var ledger = Ledger.Of([intake, move, voidMove]);

        Assert.Equal([intake], ledger.InEffect);
        Assert.Same(intake, ledger.Latest);
        Assert.Equal(Kennel, ledger.Project().LocationId);
    }

    [Fact]
    public void Project_without_the_latest_gives_the_state_a_void_restores()
    {
        var intake = Intake(T0);
        var outcome = Outcome(T0.AddDays(1));
        var ledger = Ledger.Of([intake, outcome]);

        var restored = ledger.Project(without: outcome.Id);

        Assert.Equal(new AnimalCustodyState(CustodyStatuses.InCare, Kennel, T0, intake.Id, null), restored);
    }

    [Fact]
    public void A_void_cannot_be_voided()
    {
        var voidIntake = Movement.Void(Intake(T0), "Duplicate", T0, User);

        Assert.Throws<ArgumentException>(() => Movement.Void(voidIntake, "Again", T0, User));
    }
}
