using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Host.Middleware;

/// <summary>
/// Resolves the request's tenant through the registered <see cref="ITenantResolver"/> and stores it in the scoped
/// <see cref="TenantContext"/>. No tenant resolved → the context stays empty and tenant data is invisible.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, TenantContext tenantContext)
    {
        if (await resolver.ResolveAsync(context) is { } tenantId)
        {
            tenantContext.Set(tenantId);
        }

        await next(context);
    }
}
