using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Movements.Authorization;

/// <summary>Permissions owned by the Movements module (ADR 0018).</summary>
internal static class MovementPermissions
{
    /// <summary>Read movements and their reference data (intake reasons, outcome types).</summary>
    public const string Read = "movement.read";

    /// <summary>Record intakes, relocations and outcomes.</summary>
    public const string Write = "movement.write";

    /// <summary>Void the latest movement of an animal. Sensitive: it rewrites custody after the fact.</summary>
    public const string Amend = "movement.amend";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(Read, PermissionKind.Read),
        new(Write, PermissionKind.Write),
        new(Amend, PermissionKind.Write, Sensitive: true),
    ];
}
