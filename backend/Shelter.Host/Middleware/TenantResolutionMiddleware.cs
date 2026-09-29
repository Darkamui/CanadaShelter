using System.Security.Claims;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Host.Middleware;

/// <summary>
/// Stores the signed-in account in the scoped <see cref="UserContext"/> (and as the audit actor), then resolves the
/// request's tenant through the registered <see cref="ITenantResolver"/> into the scoped <see cref="TenantContext"/>.
/// No tenant resolved → the context stays empty and tenant data is invisible.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver resolver,
        TenantContext tenantContext,
        UserContext userContext,
        AuditContext auditContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && userId != Guid.Empty)
        {
            userContext.Set(userId);
            auditContext.Set(AuditActorType.User, userId, auditContext.Source, auditContext.CorrelationId);
        }

        if (await resolver.ResolveAsync(context) is { } tenantId)
        {
            tenantContext.Set(tenantId);
        }

        await next(context);
    }
}
