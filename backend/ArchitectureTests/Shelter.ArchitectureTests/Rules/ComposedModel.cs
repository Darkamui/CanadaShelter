using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.ArchitectureTests.Rules;

/// <summary>
/// The real composed EF model: every <see cref="IModelContributor"/> in the module assemblies the host references.
/// Building a model never opens a connection.
/// </summary>
internal static class ComposedModel
{
    public static IModel Load() => Build(ModuleContributors());

    public static IModel Build(IEnumerable<IModelContributor> contributors)
    {
        var options = PersistenceServiceCollectionExtensions
            .Configure(new DbContextOptionsBuilder<ShelterDbContext>(), "Host=localhost;Database=model_only")
            .Options;

        using var context = new ShelterDbContext((DbContextOptions<ShelterDbContext>)options, new TenantContext(), contributors);
        return context.Model;
    }

    private static IEnumerable<IModelContributor> ModuleContributors() =>
        typeof(Program).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is not null && ProjectName.Parse(name.Name).Kind == ProjectKind.Module)
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IModelContributor).IsAssignableFrom(type))
            .Select(type => (IModelContributor)Activator.CreateInstance(type, nonPublic: true)!);
}
