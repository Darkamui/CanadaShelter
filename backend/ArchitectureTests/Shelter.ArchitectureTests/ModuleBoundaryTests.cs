using Shelter.ArchitectureTests.Rules;

namespace Shelter.ArchitectureTests;

/// <summary>Enforces the boundary rules on the real solution.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] Modules =
        ["Animals", "People", "Movements", "Medical", "Operations", "Engagement", "Municipal", "Reporting", "Platform"];

    private static readonly Lazy<DependencyGraph> Graph = new(SolutionGraph.Load);

    [Fact]
    public void Graph_covers_host_building_blocks_and_every_module()
    {
        var projects = Graph.Value.References.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Shelter.Host", projects);
        Assert.Contains("Shelter.BuildingBlocks", projects);
        Assert.All(Modules, module =>
        {
            Assert.Contains($"Shelter.Modules.{module}", projects);
            Assert.Contains($"Shelter.Modules.{module}.Contracts", projects);
        });

        // Guards against a silently empty graph (e.g. csproj parsing broke).
        Assert.Contains("Shelter.Modules.Animals.Contracts", Graph.Value.References["Shelter.Modules.Animals"]);
    }

    [Fact]
    public void Projects_respect_module_boundaries()
    {
        var violations = BoundaryRules.FindViolations(Graph.Value);

        Assert.True(violations.Count == 0, "Boundary violations:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void Host_contains_no_domain_types()
    {
        var violations = HostTypeRules.FindViolations(typeof(Program).Assembly.GetTypes());

        Assert.True(violations.Count == 0, "Types outside Shelter.Host{,.Composition,.Middleware}:\n" + string.Join('\n', violations));
    }
}
