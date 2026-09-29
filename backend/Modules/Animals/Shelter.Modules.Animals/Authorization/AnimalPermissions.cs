using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Animals.Authorization;

/// <summary>Permissions owned by the Animals module (ADR 0018).</summary>
internal static class AnimalPermissions
{
    /// <summary>Read animals and their reference data (species).</summary>
    public const string Read = "animal.read";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(Read, PermissionKind.Read),
    ];
}
