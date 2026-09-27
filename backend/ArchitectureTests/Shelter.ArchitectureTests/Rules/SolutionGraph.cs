using System.Reflection;
using System.Xml.Linq;

namespace Shelter.ArchitectureTests.Rules;

/// <summary>Builds the real dependency graph of the backend (test projects excluded).</summary>
internal static class SolutionGraph
{
    /// <summary>
    /// Union of declared <c>ProjectReference</c>s (catches unused references) and compiled assembly
    /// references (catches actual usage).
    /// </summary>
    public static DependencyGraph Load()
    {
        var references = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var csproj in FindProjects())
        {
            var name = Path.GetFileNameWithoutExtension(csproj);
            var edges = new HashSet<string>(DeclaredReferences(csproj), StringComparer.Ordinal);
            edges.UnionWith(CompiledReferences(name));
            references[name] = edges;
        }

        return new DependencyGraph(references);
    }

    private static IEnumerable<string> FindProjects() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "backend"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => ProjectName.Parse(Path.GetFileNameWithoutExtension(path)).Kind is not (ProjectKind.Other or ProjectKind.Tests));

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static IEnumerable<string> DeclaredReferences(string csproj) =>
        XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')));

    // Every production project is reachable from Shelter.Host, so its assembly sits next to this test.
    private static IEnumerable<string> CompiledReferences(string assemblyName) =>
        Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("Shelter.", StringComparison.Ordinal));
}
