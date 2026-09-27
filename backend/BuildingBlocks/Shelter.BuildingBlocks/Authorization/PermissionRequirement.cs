using Microsoft.AspNetCore.Authorization;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Met when the active membership's roles grant <see cref="Permission"/>.</summary>
/// <param name="Permission">Permission name.</param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Evaluates <see cref="PermissionRequirement"/> against the request's <see cref="IPermissionContext"/>. Scoped.</summary>
public sealed class PermissionAuthorizationHandler(IPermissionContext permissions) : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        if (context.User.Identity?.IsAuthenticated == true && permissions.Has(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
