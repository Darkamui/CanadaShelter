using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// An account's staff access to one organization (architecture §9.3, ADR 0018). The only thing that gives an
/// account tenant access: an email address, an external participant record or a platform-operator flag never does.
/// Tenant-owned, with a self-read policy so an account can list its own memberships before choosing one. Its roles
/// are system role keys (<see cref="SystemRoles"/>) on the row itself, so one read resolves tenant and permissions.
/// </summary>
internal sealed class StaffMembership : ITenantOwned
{
    private StaffMembership()
    {
    }

    public StaffMembership(Guid userId, IEnumerable<string> roleKeys, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        Id = Guid.CreateVersion7();
        UserId = userId;
        Status = StaffMembershipStatus.Active;
        RoleKeys = Validate(roleKeys);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>The <see cref="UserAccount"/>. An ID, not a foreign key, so every index can start with the tenant.</summary>
    public Guid UserId { get; private set; }

    public StaffMembershipStatus Status { get; private set; }

    /// <summary>System role keys, sorted and distinct. At least one.</summary>
    public string[] RoleKeys { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Whether this membership currently grants administrator access.</summary>
    public bool IsActiveAdministrator =>
        Status == StaffMembershipStatus.Active && RoleKeys.Contains(SystemRoles.Administrator, StringComparer.Ordinal);

    /// <summary>Replaces the roles. Callers guard the last active administrator.</summary>
    /// <exception cref="ArgumentException">A key is not a system role, or none is given.</exception>
    public void ChangeRoles(IEnumerable<string> roleKeys) => RoleKeys = Validate(roleKeys);

    /// <summary>Ends access; the row stays for history. Callers guard the last active administrator.</summary>
    public void Suspend() => Status = StaffMembershipStatus.Suspended;

    /// <summary>Restores access with the roles it had.</summary>
    public void Reactivate() => Status = StaffMembershipStatus.Active;

    /// <summary>Whether <paramref name="roleKeys"/> would be accepted.</summary>
    public static bool AreValid(IEnumerable<string>? roleKeys) =>
        roleKeys is not null && roleKeys.Any() && roleKeys.All(k => k is not null && SystemRoles.All.Contains(k));

    private static string[] Validate(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);
        var keys = roleKeys.ToList();
        if (!AreValid(keys))
        {
            throw new ArgumentException("Roles must be one or more system role keys.", nameof(roleKeys));
        }

        return [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}

/// <summary>Whether a membership grants access. Stored by name.</summary>
internal enum StaffMembershipStatus
{
    /// <summary>Grants access to the organization.</summary>
    Active,

    /// <summary>Kept for history; grants nothing.</summary>
    Suspended,
}
