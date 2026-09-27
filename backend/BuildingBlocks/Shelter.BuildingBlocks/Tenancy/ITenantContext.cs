namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>The tenant of the current unit of work (request, job, import). Scoped.</summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or <see langword="null"/> when none was resolved (fail closed: no tenant data).</summary>
    Guid? TenantId { get; }
}

/// <summary>
/// Scoped holder of the current tenant. Set once per scope by the tenant resolution middleware or the job runner;
/// never from a request body.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <summary>Sets the tenant. A scope belongs to exactly one tenant: setting a different one throws.</summary>
    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID must not be empty.", nameof(tenantId));
        }

        if (TenantId is { } current && current != tenantId)
        {
            throw new InvalidOperationException("The tenant of a scope cannot change.");
        }

        TenantId = tenantId;
    }
}

/// <summary>Helpers over <see cref="ITenantContext"/>.</summary>
public static class TenantContextExtensions
{
    /// <summary>The current tenant; throws <see cref="TenantContextMissingException"/> when there is none.</summary>
    public static Guid RequireTenantId(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TenantId ?? throw new TenantContextMissingException();
    }
}
