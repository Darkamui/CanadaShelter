using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Sets <see cref="ITenantOwned.TenantId"/> on insert from the context's <see cref="ITenantContext"/>, and rejects
/// any insert, update or delete of another tenant's row, or any tenant-owned write without a tenant.
/// </summary>
internal sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    public static TenantStampingInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is not ShelterDbContext shelter)
        {
            return;
        }

        shelter.ChangeTracker.DetectChanges();
        foreach (var entry in shelter.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                Check(entry, shelter.TenantContext.RequireTenantId());
            }
        }
    }

    private static void Check(EntityEntry<ITenantOwned> entry, Guid tenantId)
    {
        var property = entry.Property(e => e.TenantId);

        if (entry.State == EntityState.Added)
        {
            if (property.CurrentValue == Guid.Empty)
            {
                property.CurrentValue = tenantId;
            }
            else if (property.CurrentValue != tenantId)
            {
                throw new TenantIsolationException();
            }

            return;
        }

        if (property.OriginalValue != tenantId || property.CurrentValue != tenantId)
        {
            throw new TenantIsolationException();
        }
    }
}
