using System.Reflection;
using System.Text.RegularExpressions;
using Shelter.BuildingBlocks.Persistence;

namespace Shelter.ArchitectureTests.Rules;

/// <summary>Rules protecting the tenant transaction and the platform-admin path (ADR 0004, architecture §8).</summary>
internal static partial class PersistenceRules
{
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <returns>Lines that set a tenant setting for the whole session instead of the transaction.</returns>
    /// <remarks>A session-level setting survives on a pooled connection and leaks into the next request.</remarks>
    public static IReadOnlyList<string> FindSessionTenantSettings(IEnumerable<(string Path, string Text)> sources) =>
        [.. sources.SelectMany(source => source.Text
            .Split('\n')
            .Select((line, index) => (line, number: index + 1))
            .Where(l => SessionSet().IsMatch(l.line) || SessionSetConfig().IsMatch(l.line))
            .Select(l => $"{source.Path}:{l.number}"))];

    /// <returns>Types outside the Platform module that take or hold a <see cref="PlatformAdminDbContextFactory"/>.</returns>
    public static IReadOnlyList<string> FindPlatformAdminFactoryDependents(IEnumerable<Type> types) =>
        [.. types
            .Where(type => type.Assembly.GetName().Name != "Shelter.Modules.Platform")
            .Where(type => type != typeof(PlatformAdminDbContextFactory) && DependsOnPlatformAdminFactory(type))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)];

    private static bool DependsOnPlatformAdminFactory(Type type) =>
        type.GetConstructors(AllMembers).SelectMany(c => c.GetParameters()).Any(p => p.ParameterType == typeof(PlatformAdminDbContextFactory))
        || type.GetFields(AllMembers).Any(f => f.FieldType == typeof(PlatformAdminDbContextFactory))
        || type.GetProperties(AllMembers).Any(p => p.PropertyType == typeof(PlatformAdminDbContextFactory));

    // "SET app.x" or "SET SESSION app.x"; "SET LOCAL app.x" is the allowed form.
    [GeneratedRegex(@"\bSET\s+(SESSION\s+)?app\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionSet();

    // set_config('app.x', value, false): is_local = false is session-wide.
    [GeneratedRegex(@"set_config\s*\(\s*'app\.[^']*'\s*,.*,\s*false\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionSetConfig();
}
