using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// The single composed DbContext (ADR 0005). It knows no module types: the model comes from the registered
/// <see cref="IModelContributor"/>s. Modules use <c>Set&lt;T&gt;()</c> for their own entities only.
/// </summary>
public sealed class ShelterDbContext : DbContext
{
    /// <summary>Name of the global query filter on every <see cref="ITenantOwned"/> entity.</summary>
    public const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(ShelterDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IReadOnlyList<IModelContributor> _contributors;

    /// <summary>Creates the context.</summary>
    public ShelterDbContext(DbContextOptions<ShelterDbContext> options, ITenantContext tenantContext, IEnumerable<IModelContributor> contributors)
        : base(options)
    {
        TenantContext = tenantContext;
        _contributors = [.. contributors.OrderBy(c => c.GetType().FullName, StringComparer.Ordinal)];
        ModelKey = string.Join('|', _contributors.Select(c => c.GetType().FullName));

        // EF skips the transaction for a single-statement SaveChanges; without one, SET LOCAL has nothing to apply to.
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
    }

    /// <summary>The tenant this context reads and writes for.</summary>
    public ITenantContext TenantContext { get; }

    /// <summary>
    /// True for contexts from <see cref="PlatformAdminDbContextFactory"/> only: no <c>SET LOCAL</c>, no transaction
    /// guard, and writes keep an explicit <see cref="ITenantOwned.TenantId"/> instead of the context's tenant.
    /// </summary>
    internal bool IsPlatformAdmin { get; init; }

    /// <summary>Identifies the contributor set, so differently composed contexts get their own cached model.</summary>
    internal string ModelKey { get; }

    // Read by the tenant query filter; EF evaluates it per query against this instance.
    private Guid? CurrentTenantId => TenantContext.TenantId;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var contributor in _contributors)
        {
            contributor.ConfigureModel(modelBuilder);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (!entityType.IsOwned() && entityType.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    // No tenant → no rows. RLS is the database-side backstop for the same rule (ADR 0003).
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        Expression<Func<TEntity, bool>> filter = e => CurrentTenantId != null && e.TenantId == CurrentTenantId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(TenantFilter, filter);
        modelBuilder.Entity<TEntity>().Property(e => e.TenantId).IsRequired();
    }
}
