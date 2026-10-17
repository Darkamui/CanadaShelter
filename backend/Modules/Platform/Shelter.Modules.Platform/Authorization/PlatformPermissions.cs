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

    /// <summary>
    /// Fill an organization with sample data (ADR 0023). Declared only where the route exists (Development with
    /// <c>DevSeed:Enabled</c>). Sensitive: administrators only.
    /// </summary>
    public const string DevSeed = "platform.dev.seed";

    public static PermissionDefinition DevSeedDefinition { get; } = new(DevSeed, PermissionKind.Write, Sensitive: true);

    public static IEnumerable<PermissionDefinition> All { get; } =
    [
        new(StaffRead, PermissionKind.Read, Sensitive: true),
        new(StaffManage, PermissionKind.Write, Sensitive: true),

        // Declared here because the audit trail is platform infrastructure; the name lives with IAuditReader.
        new(AuditPermissions.Read, PermissionKind.Read, Sensitive: true),
    ];
}
