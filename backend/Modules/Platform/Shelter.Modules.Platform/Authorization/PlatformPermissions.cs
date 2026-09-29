using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Authorization;

namespace Shelter.Modules.Platform.Authorization;

/// <summary>Permissions owned by the Platform module (ADR 0018).</summary>
internal static class PlatformPermissions
{
    /// <summary>List the organization's staff: names, emails, roles, status. Sensitive: colleagues' emails (Law 25 minimization).</summary>
    public const string StaffRead = "platform.staff.read";

    /// <summary>Change staff roles, suspend and reactivate. Sensitive: it could grant itself anything.</summary>
    public const string StaffManage = "platform.staff.manage";

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(StaffRead, PermissionKind.Read, Sensitive: true),
        new(StaffManage, PermissionKind.Write, Sensitive: true),

        // Declared here because the audit trail is platform infrastructure; the name lives with IAuditReader.
        new(AuditPermissions.Read, PermissionKind.Read, Sensitive: true),
    ];
}
