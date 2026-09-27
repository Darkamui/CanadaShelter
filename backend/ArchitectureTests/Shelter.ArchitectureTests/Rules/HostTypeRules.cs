using System.Runtime.CompilerServices;

namespace Shelter.ArchitectureTests.Rules;

/// <summary>The host only composes modules: no domain, feature, or persistence types live in it.</summary>
internal static class HostTypeRules
{
    private static readonly HashSet<string> AllowedNamespaces = new(StringComparer.Ordinal)
    {
        "Shelter.Host",
        "Shelter.Host.Composition",
        "Shelter.Host.Middleware",
    };

    /// <returns>Full names of types declared outside the allowed host namespaces.</returns>
    public static IReadOnlyList<string> FindViolations(IEnumerable<Type> types) =>
        [.. types
            .Where(IsAuthoredType)
            .Where(type => !IsAllowed(type))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)];

    // Ignore compiler output and source-generator types outside our namespaces (e.g. OpenAPI generator).
    private static bool IsAuthoredType(Type type) =>
        !IsCompilerGenerated(type)
        && (type.Namespace is null || type.Namespace.StartsWith("Shelter", StringComparison.Ordinal));

    // Also covers types nested in compiler-synthesized ones (e.g. <>z__ReadOnlySingleElementList`1+Enumerator).
    private static bool IsCompilerGenerated(Type type)
    {
        for (Type? current = type; current is not null; current = current.DeclaringType)
        {
            if (current.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) || current.Name.StartsWith('<'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowed(Type type) =>
        type.Namespace is null
            ? type.Name == "Program"
            : AllowedNamespaces.Contains(type.Namespace);
}
