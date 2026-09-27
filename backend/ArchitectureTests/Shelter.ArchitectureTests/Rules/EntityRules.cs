using Microsoft.EntityFrameworkCore.Metadata;

namespace Shelter.ArchitectureTests.Rules;

/// <summary>Rules over the composed EF model (ADR 0005).</summary>
internal static class EntityRules
{
    /// <returns>Entity types declared in a module assembly and visible outside it.</returns>
    /// <remarks>Entities are module internals; other modules see contracts, events and IDs only (hard rule 7).</remarks>
    public static IReadOnlyList<string> FindPublicModuleEntities(IModel model) =>
        [.. model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => type.Assembly.GetName().Name is { } assembly && ProjectName.Parse(assembly).Kind == ProjectKind.Module)
            .Where(type => type.IsVisible)
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
}
