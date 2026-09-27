using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Auditing;
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

    /// <summary>
    /// Creates the context. Without an <paramref name="auditContext"/>, changes are audited as the system actor;
    /// without a <paramref name="keyProvider"/>, personal values in audit payloads are redacted, not encrypted;
    /// without a <paramref name="userContext"/>, no account is set for self-read policies.
    /// </summary>
    public ShelterDbContext(
        DbContextOptions<ShelterDbContext> options,
        ITenantContext tenantContext,
        IEnumerable<IModelContributor> contributors,
        AuditContext? auditContext = null,
        IKeyProvider? keyProvider = null,
        IUserContext? userContext = null)
        : base(options)
    {
        TenantContext = tenantContext;
        UserContext = userContext ?? new UserContext();
        AuditContext = auditContext ?? new AuditContext();
        KeyProvider = keyProvider;
        _contributors = [.. contributors.OrderBy(c => c.GetType().FullName, StringComparer.Ordinal)];
        ModelKey = string.Join('|', _contributors.Select(c => c.GetType().FullName));

        // EF skips the transaction for a single-statement SaveChanges; without one, SET LOCAL has nothing to apply to.
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
    }

    /// <summary>The tenant this context reads and writes for.</summary>
    public ITenantContext TenantContext { get; }

    /// <summary>The signed-in account, set as <c>app.user_id</c> for self-read policies (never for tenant access).</summary>
    public IUserContext UserContext { get; }

    /// <summary>Actor, source and correlation stamped on the audit events this context records.</summary>
    public AuditContext AuditContext { get; }

    /// <summary>Wraps the per-person data keys that encrypt personal audit values.</summary>
    internal IKeyProvider? KeyProvider { get; }

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
        AuditEvent.Configure(modelBuilder);
        PersonDataKey.Configure(modelBuilder);

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
