using Microsoft.AspNetCore.Http;

namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>
/// Resolves the tenant of an HTTP request. The Platform module registers the membership-based resolver.
/// Returning <see langword="null"/> means "no tenant" (fail closed).
/// </summary>
public interface ITenantResolver
{
    /// <summary>The request's tenant, or <see langword="null"/>.</summary>
    ValueTask<Guid?> ResolveAsync(HttpContext context);
}

/// <summary>Resolves no tenant. The fallback when no module registers a resolver.</summary>
public sealed class NoTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context) => ValueTask.FromResult<Guid?>(null);
}
