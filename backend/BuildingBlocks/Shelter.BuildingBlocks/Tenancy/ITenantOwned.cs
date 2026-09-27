namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>
/// A row that belongs to one organization (tenant). Its table has a <c>tenant_id</c> column, an RLS policy,
/// and indexes that start with <c>tenant_id</c> (architecture §8.2, ADR 0003).
/// </summary>
/// <remarks>
/// Entities expose <see cref="TenantId"/> with a private setter. Only the persistence interceptor sets it,
/// from <see cref="ITenantContext"/>; handlers and request bodies never do (CLAUDE.md hard rule 2).
/// </remarks>
public interface ITenantOwned
{
    /// <summary>The owning organization's ID.</summary>
    Guid TenantId { get; }
}
