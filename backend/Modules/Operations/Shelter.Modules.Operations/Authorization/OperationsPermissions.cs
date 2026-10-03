using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Operations.Authorization;

/// <summary>Permissions owned by the Operations module (ADR 0018).</summary>
internal static class OperationsPermissions
{
    /// <summary>Read the location tree and the location kind list.</summary>
    public const string LocationRead = "location.read";

    /// <summary>Create, rename, move and archive locations.</summary>
    public const string LocationWrite = "location.write";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(LocationRead, PermissionKind.Read),
        new(LocationWrite, PermissionKind.Write),
    ];
}
