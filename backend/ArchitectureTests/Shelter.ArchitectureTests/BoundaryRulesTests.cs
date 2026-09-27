using Shelter.ArchitectureTests.Rules;

namespace Shelter.ArchitectureTests;

/// <summary>Deliberate violations on synthetic graphs: proves each rule actually catches what it claims.</summary>
public sealed class BoundaryRulesTests
{
    private static DependencyGraph GraphOf(params (string From, string[] To)[] edges) =>
        new(edges.ToDictionary(
            edge => edge.From,
            edge => (IReadOnlySet<string>)edge.To.ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal));

    [Fact]
    public void Module_referencing_another_modules_internals_is_a_violation()
    {
        var graph = GraphOf(("Shelter.Modules.Animals", ["Shelter.BuildingBlocks", "Shelter.Modules.People"]));

        var violation = Assert.Single(BoundaryRules.FindViolations(graph));

        Assert.Equal(
            new BoundaryViolation("Shelter.Modules.Animals", "Shelter.Modules.People", BoundaryRules.ModuleToModuleInternals),
            violation);
    }

    [Fact]
    public void Module_referencing_contracts_and_its_own_parts_is_allowed()
    {
        var graph = GraphOf(
            ("Shelter.Modules.Animals", ["Shelter.BuildingBlocks", "Shelter.Modules.Animals.Contracts", "Shelter.Modules.People.Contracts", "Shelter.Modules.Animals.Infrastructure"]),
            ("Shelter.Modules.Animals.Contracts", ["Shelter.BuildingBlocks", "Shelter.Modules.People.Contracts"]),
            ("Shelter.Host", ["Shelter.Modules.Animals", "Shelter.Modules.People"]));

        Assert.Empty(BoundaryRules.FindViolations(graph));
    }

    [Theory]
    [InlineData("Shelter.Modules.Animals")]
    [InlineData("Shelter.Modules.People")]
    [InlineData("Shelter.Host")]
    public void Contracts_referencing_implementation_is_a_violation(string target)
    {
        var graph = GraphOf(("Shelter.Modules.Animals.Contracts", [target]));

        var violation = Assert.Single(BoundaryRules.FindViolations(graph));

        Assert.Equal(BoundaryRules.ContractsTooWide, violation.Rule);
    }

    [Theory]
    [InlineData("Shelter.Modules.Animals")]
    [InlineData("Shelter.Modules.Animals.Contracts")]
    [InlineData("Shelter.Host")]
    public void BuildingBlocks_referencing_modules_is_a_violation(string target)
    {
        var graph = GraphOf(("Shelter.BuildingBlocks", [target]));

        var violation = Assert.Single(BoundaryRules.FindViolations(graph));

        Assert.Equal(BoundaryRules.BuildingBlocksToModules, violation.Rule);
    }

    [Fact]
    public void Module_referencing_host_is_a_violation()
    {
        var graph = GraphOf(("Shelter.Modules.Animals", ["Shelter.Host"]));

        var violation = Assert.Single(BoundaryRules.FindViolations(graph));

        Assert.Equal(BoundaryRules.ModuleToHost, violation.Rule);
    }
}
