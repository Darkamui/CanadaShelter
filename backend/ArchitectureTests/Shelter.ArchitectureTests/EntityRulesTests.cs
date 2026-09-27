using Microsoft.EntityFrameworkCore;
using Shelter.ArchitectureTests.Rules;

namespace Shelter.ArchitectureTests;

/// <summary>Enforces the entity rules on the real composed model.</summary>
public sealed class EntityRulesTests
{
    [Fact]
    public void Composed_model_includes_module_entities()
    {
        // Guards against a silently empty model (e.g. contributor discovery broke).
        Assert.Contains(ComposedModel.Load().GetEntityTypes(), entityType => entityType.GetTableName() == "organization");
    }

    [Fact]
    public void Module_entity_types_are_not_public()
    {
        var violations = EntityRules.FindPublicModuleEntities(ComposedModel.Load());

        Assert.True(violations.Count == 0, "Public entity types in module assemblies:\n" + string.Join('\n', violations));
    }
}
