using Microsoft.AspNetCore.Authorization;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>
/// Met only when the request resolved a tenant, i.e. the signed-in account has an active membership in its active
/// organization (ADR 0018). Without one, the request is denied (403 for a signed-in user).
/// </summary>
public sealed class TenantRequirement : IAuthorizationRequirement
{
    /// <summary>The shared instance.</summary>
    public static TenantRequirement Instance { get; } = new();
}

/// <summary>Evaluates <see cref="TenantRequirement"/> against the request's <see cref="ITenantContext"/>. Scoped.</summary>
public sealed class TenantAuthorizationHandler(ITenantContext tenantContext) : AuthorizationHandler<TenantRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.TenantId is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
