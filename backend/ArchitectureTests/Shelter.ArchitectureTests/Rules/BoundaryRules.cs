namespace Shelter.ArchitectureTests.Rules;

/// <summary>Project → referenced projects (only Shelter.* names matter).</summary>
internal sealed record DependencyGraph(IReadOnlyDictionary<string, IReadOnlySet<string>> References);

internal sealed record BoundaryViolation(string From, string To, string Rule)
{
    public override string ToString() => $"{From} -> {To}: {Rule}";
}

/// <summary>Module boundary rules (architecture §5.3, CLAUDE.md hard rule 7).</summary>
internal static class BoundaryRules
{
    public const string ModuleToModuleInternals = "Modules may reference other modules only through their .Contracts project";
    public const string ContractsTooWide = "Contracts may reference only BuildingBlocks and other .Contracts projects";
    public const string BuildingBlocksToModules = "BuildingBlocks must not reference modules or the host";
    public const string ModuleToHost = "Modules must not reference the host";

    public static IReadOnlyList<BoundaryViolation> FindViolations(DependencyGraph graph)
    {
        var violations = new List<BoundaryViolation>();

        foreach (var (fromName, references) in graph.References)
        {
            var from = ProjectName.Parse(fromName);
            foreach (var toName in references)
            {
                var to = ProjectName.Parse(toName);
                var rule = (from.Kind, to.Kind) switch
                {
                    (ProjectKind.BuildingBlocks, ProjectKind.Module or ProjectKind.Contracts or ProjectKind.Host) => BuildingBlocksToModules,
                    (ProjectKind.Contracts, ProjectKind.Module or ProjectKind.Host) => ContractsTooWide,
                    (ProjectKind.Module, ProjectKind.Module) when from.Module != to.Module => ModuleToModuleInternals,
                    (ProjectKind.Module, ProjectKind.Host) => ModuleToHost,
                    _ => null,
                };

                if (rule is not null)
                {
                    violations.Add(new BoundaryViolation(fromName, toName, rule));
                }
            }
        }

        return violations;
    }
}
