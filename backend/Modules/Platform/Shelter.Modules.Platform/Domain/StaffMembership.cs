using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// An account's staff access to one organization (architecture §9.3, ADR 0018). The only thing that gives an
/// account tenant access: an email address, an external participant record or a platform-operator flag never does.
/// Tenant-owned, with a self-read policy so an account can list its own memberships before choosing one.
/// </summary>
internal sealed class StaffMembership : ITenantOwned
{
    private StaffMembership()
    {
    }

    public StaffMembership(Guid userId, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        Id = Guid.CreateVersion7();
        UserId = userId;
        Status = StaffMembershipStatus.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>The <see cref="UserAccount"/>. An ID, not a foreign key, so every index can start with the tenant.</summary>
    public Guid UserId { get; private set; }

    public StaffMembershipStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>Whether a membership grants access. Stored by name.</summary>
internal enum StaffMembershipStatus
{
    /// <summary>Grants access to the organization.</summary>
    Active,

    /// <summary>Kept for history; grants nothing.</summary>
    Suspended,
}
