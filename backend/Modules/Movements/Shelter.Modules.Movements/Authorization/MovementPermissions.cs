using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Movements.Authorization;

/// <summary>Permissions owned by the Movements module (ADR 0018).</summary>
internal static class MovementPermissions
{
    /// <summary>Read movements and their reference data (intake reasons).</summary>
    public const string Read = "movement.read";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(Read, PermissionKind.Read),
    ];
}
