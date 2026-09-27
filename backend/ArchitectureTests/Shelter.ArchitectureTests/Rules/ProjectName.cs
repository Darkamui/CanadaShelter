namespace Shelter.ArchitectureTests.Rules;

internal enum ProjectKind
{
    Other,
    Host,
    BuildingBlocks,
    Module,
    Contracts,
    Migrations,

    /// <summary>Test and test-support projects; not part of the production graph.</summary>
    Tests,
}

/// <summary>Classifies a project (= assembly) name by the backend layout convention (ADR 0015).</summary>
internal readonly record struct ProjectName(string Name, ProjectKind Kind, string? Module)
{
    private const string ModulePrefix = "Shelter.Modules.";
    private const string ContractsSuffix = ".Contracts";

    public static ProjectName Parse(string name)
    {
        if (name == "Shelter.Host")
        {
            return new(name, ProjectKind.Host, null);
        }

        if (name == "Shelter.Migrations")
        {
            return new(name, ProjectKind.Migrations, null);
        }

        // Checked before the module prefix: Shelter.Modules.Platform.Tests is a test project, not part of Platform.
        if (name == "Shelter.Testing" || name.EndsWith(".Tests", StringComparison.Ordinal) || name.EndsWith(".IntegrationTests", StringComparison.Ordinal))
        {
            return new(name, ProjectKind.Tests, null);
        }

        if (name == "Shelter.BuildingBlocks" || name.StartsWith("Shelter.BuildingBlocks.", StringComparison.Ordinal))
        {
            return new(name, ProjectKind.BuildingBlocks, null);
        }

        if (name.StartsWith(ModulePrefix, StringComparison.Ordinal))
        {
            var rest = name[ModulePrefix.Length..];
            if (rest.EndsWith(ContractsSuffix, StringComparison.Ordinal))
            {
                return new(name, ProjectKind.Contracts, rest[..^ContractsSuffix.Length]);
            }

            // Shelter.Modules.Animals, or a future Shelter.Modules.Animals.<Part> of the same module.
            var dot = rest.IndexOf('.', StringComparison.Ordinal);
            return new(name, ProjectKind.Module, dot < 0 ? rest : rest[..dot]);
        }

        return new(name, ProjectKind.Other, null);
    }
}
