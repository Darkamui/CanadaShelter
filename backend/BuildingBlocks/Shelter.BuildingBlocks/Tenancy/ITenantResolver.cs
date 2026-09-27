using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>
/// Resolves the tenant of an HTTP request. Replaceable: M2 swaps in a resolver based on the authenticated
/// membership. Returning <see langword="null"/> means "no tenant" (fail closed).
/// </summary>
public interface ITenantResolver
{
    /// <summary>The request's tenant, or <see langword="null"/>.</summary>
    ValueTask<Guid?> ResolveAsync(HttpContext context);
}

/// <summary>Resolves no tenant. The default outside Development until identity exists (M2).</summary>
public sealed class NoTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context) => ValueTask.FromResult<Guid?>(null);
}

/// <summary>
/// Development only: reads the tenant from the <c>X-Tenant-Id</c> header. Refuses to exist in any other
/// environment, so a misregistration fails at startup instead of trusting a client header in production.
/// </summary>
public sealed class DevelopmentHeaderTenantResolver : ITenantResolver
{
    /// <summary>The request header carrying the tenant ID.</summary>
    public const string HeaderName = "X-Tenant-Id";

    /// <summary>Creates the resolver; throws outside Development.</summary>
    public DevelopmentHeaderTenantResolver(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{nameof(DevelopmentHeaderTenantResolver)} is only allowed in Development.");
        }
    }

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var header = context.Request.Headers[HeaderName].ToString();
        return ValueTask.FromResult<Guid?>(Guid.TryParseExact(header, "D", out var tenantId) && tenantId != Guid.Empty ? tenantId : null);
    }
}
