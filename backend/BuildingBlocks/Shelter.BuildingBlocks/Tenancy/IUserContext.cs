namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>
/// The signed-in account of the current unit of work, for row-level security only (ADR 0018): the unit of work
/// sets it as <c>app.user_id</c> so self-read policies (a user's own memberships) work before a tenant is chosen.
/// It grants nothing by itself. Scoped.
/// </summary>
public interface IUserContext
{
    /// <summary>The signed-in account, or <see langword="null"/> for anonymous requests and jobs.</summary>
    Guid? UserId { get; }
}

/// <summary>Scoped holder of the current account. Set once per scope, from the authenticated principal only.</summary>
public sealed class UserContext : IUserContext
{
    /// <inheritdoc />
    public Guid? UserId { get; private set; }

    /// <summary>Sets the account. A scope belongs to one account: setting a different one throws.</summary>
    public void Set(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        if (UserId is { } current && current != userId)
        {
            throw new InvalidOperationException("The user of a scope cannot change.");
        }

        UserId = userId;
    }
}
