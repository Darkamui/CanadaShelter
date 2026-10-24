using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Animals.Authorization;

/// <summary>Permissions owned by the Animals module (ADR 0018).</summary>
internal static class AnimalPermissions
{
    /// <summary>Read animals, their identifiers and timeline, and their reference data (species).</summary>
    public const string Read = "animal.read";

    /// <summary>Register and edit animals and their identifiers.</summary>
    public const string Write = "animal.write";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(Read, PermissionKind.Read),
        new(Write, PermissionKind.Write),
    ];
}
