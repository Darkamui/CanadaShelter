using System.Reflection;
using Shelter.ArchitectureTests.Rules;
using Shelter.BuildingBlocks.Persistence;

namespace Shelter.ArchitectureTests;

/// <summary>Enforces the persistence rules on the real sources and assemblies, and checks them on synthetic cases.</summary>
public sealed class PersistenceRulesTests
{
    [Fact]
    public void No_source_sets_a_tenant_setting_for_the_session()
    {
        var backend = Path.Combine(RepositoryPaths.Root, "backend");
        var sources = Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsExcluded(Path.GetRelativePath(backend, path)))
            .Select(path => (Path.GetRelativePath(RepositoryPaths.Root, path), File.ReadAllText(path)));

        var violations = PersistenceRules.FindSessionTenantSettings(sources);

        Assert.True(violations.Count == 0, "Session-level tenant settings:\n" + string.Join('\n', violations));
    }

    [Theory]
    [InlineData("SET app.tenant_id = 'x'")]
    [InlineData("set session app.tenant_id = 'x'")]
    [InlineData("SELECT set_config('app.tenant_id', '1', false)")]
    public void Session_level_settings_are_violations(string sql)
    {
        Assert.Single(PersistenceRules.FindSessionTenantSettings([("x.cs", sql)]));
    }

    [Theory]
    [InlineData("SET LOCAL app.tenant_id = 'x'")]
    [InlineData("SELECT set_config('app.tenant_id', '1', true)")]
    public void Transaction_level_settings_are_allowed(string sql)
    {
        Assert.Empty(PersistenceRules.FindSessionTenantSettings([("x.cs", sql)]));
    }

    [Fact]
    public void Only_the_platform_module_depends_on_the_platform_admin_factory()
    {
        var types = typeof(Program).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is not null && ProjectName.Parse(name.Name).Kind == ProjectKind.Module)
            .Select(Assembly.Load)
            .Append(typeof(Program).Assembly)
            .Append(typeof(ShelterDbContext).Assembly)
            .SelectMany(assembly => assembly.GetTypes());

        var violations = PersistenceRules.FindPlatformAdminFactoryDependents(types);

        Assert.True(violations.Count == 0, "Platform-admin factory used outside Platform:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void Platform_admin_factory_dependents_outside_platform_are_violations()
    {
        Assert.Equal(
            [typeof(FactoryParameter).FullName!, typeof(FactoryProperty).FullName!],
            PersistenceRules.FindPlatformAdminFactoryDependents([typeof(FactoryProperty), typeof(FactoryParameter), typeof(PersistenceRulesTests)]));
    }

    // This project holds the rule's synthetic violations; build output is not source.
    private static bool IsExcluded(string relativePath) =>
        relativePath.StartsWith("ArchitectureTests", StringComparison.Ordinal)
        || relativePath.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private sealed class FactoryParameter(PlatformAdminDbContextFactory factory)
    {
        public override string ToString() => factory.ToString()!;
    }

    private sealed class FactoryProperty
    {
        public PlatformAdminDbContextFactory? Factory { get; set; }
    }
}
