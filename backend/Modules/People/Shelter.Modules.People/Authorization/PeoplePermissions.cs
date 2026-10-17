using Shelter.BuildingBlocks.Authorization;
using Shelter.Modules.People.Contracts;

namespace Shelter.Modules.People.Authorization;

/// <summary>Permissions owned by the People module (ADR 0018).</summary>
internal static class PeoplePermissions
{
    /// <summary>Read people: names, contact details, addresses, notes.</summary>
    public const string Read = PeoplePermissionNames.Read;

    /// <summary>Create, update, archive and unarchive people.</summary>
    public const string Write = "person.write";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(Read, PermissionKind.Read),
        new(Write, PermissionKind.Write),
    ];
}
