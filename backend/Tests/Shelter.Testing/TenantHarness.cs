using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Persistence.Migrations;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Testing;

/// <summary>
/// Tenant-scoped access to the test database as the runtime role (<c>shelter_app</c>), composed from a module's
/// model contributors. <see cref="AssertIsolatedAsync{TEntity}"/> gives every tenant-owned entity the standard
/// isolation checks in one line:
/// <code>[Fact] public Task Widget_is_isolated() => fixture.Tenants.AssertIsolatedAsync(() => new Widget("x"));</code>
/// </summary>
public sealed class TenantHarness(string appConnectionString, params IModelContributor[] contributors)
{
    /// <summary>A fresh tenant ID. Tenant-owned tables have no FK to <c>platform.organization</c> (module boundary).</summary>
    public static Guid NewTenantId() => Guid.CreateVersion7();

    /// <summary>A runtime-role context for <paramref name="tenantId"/>, or with no tenant.</summary>
    public ShelterDbContext CreateContext(Guid? tenantId) => TestDbContexts.Create(appConnectionString, tenantId, contributors);

    /// <summary>Runs <paramref name="work"/> for <paramref name="tenantId"/> in one committed unit of work, as an endpoint does.</summary>
    public async Task<T> InTenantAsync<T>(Guid? tenantId, Func<ShelterDbContext, Task<T>> work)
    {
        await using var db = CreateContext(tenantId);
        return await db.InUnitOfWorkAsync(() => work(db));
    }

    /// <summary>Saves <paramref name="entity"/> for <paramref name="tenantId"/>; the tenant is stamped by the context.</summary>
    public async Task<TEntity> SeedAsync<TEntity>(Guid tenantId, TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class, ITenantOwned
    {
        await using var db = CreateContext(tenantId);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// Proves tenant isolation for <typeparamref name="TEntity"/> as the runtime role: another tenant's row is
    /// invisible through EF, through <c>IgnoreQueryFilters()</c> (RLS backstop) and through raw SQL in a unit of
    /// work; with no tenant, EF and raw SQL return zero rows without error; a cross-tenant update is rejected by the
    /// context, and raw <c>UPDATE</c>/<c>DELETE</c> in another tenant's unit of work affect no row (RLS write backstop).
    /// Covers one table: child tables of an aggregate each need their own call.
    /// Throws <see cref="TenantIsolationAssertionException"/> on the first failed check.
    /// </summary>
    public async Task AssertIsolatedAsync<TEntity>(Func<TEntity> create, CancellationToken cancellationToken = default)
        where TEntity : class, ITenantOwned
    {
        ArgumentNullException.ThrowIfNull(create);

        var tenantA = NewTenantId();
        var tenantB = NewTenantId();
        var seeded = await SeedAsync(tenantA, create(), cancellationToken);
        var table = TableOf<TEntity>();

        // Sanity: the owner sees its row, so the checks below cannot pass vacuously.
        Check(await CountAsync<TEntity>(tenantA, tenantA, ignoreFilters: false, cancellationToken) == 1, "the owning tenant does not see its own row");
        Check(await RawCountInUnitOfWorkAsync(tenantA, table, tenantA, cancellationToken) >= 1, "raw SQL does not see the owning tenant's row");

        Check(await CountAsync<TEntity>(tenantB, tenantA, ignoreFilters: false, cancellationToken) == 0, "another tenant sees the row through EF");
        Check(await CountAsync<TEntity>(tenantB, tenantA, ignoreFilters: true, cancellationToken) == 0, "IgnoreQueryFilters() exposes another tenant's row: RLS is missing");
        Check(await RawCountInUnitOfWorkAsync(tenantB, table, tenantA, cancellationToken) == 0, "raw SQL in another tenant's unit of work sees the row");

        Check(await CountWithoutTenantAsync<TEntity>(tenantA, cancellationToken) == 0, "a context without a tenant sees the row");
        Check(await RawCountWithoutTenantAsync(table, tenantA, cancellationToken) == 0, "raw SQL without a tenant sees the row");

        Check(await RawInUnitOfWorkAsync(tenantB, $"UPDATE {table} SET tenant_id = tenant_id WHERE tenant_id = @tenant", tenantA, cancellationToken) == 0, "raw UPDATE in another tenant's unit of work reached the row");
        Check(await RawInUnitOfWorkAsync(tenantB, $"DELETE FROM {table} WHERE tenant_id = @tenant", tenantA, cancellationToken) == 0, "raw DELETE in another tenant's unit of work reached the row");
        Check(await CountAsync<TEntity>(tenantA, tenantA, ignoreFilters: false, cancellationToken) == 1, "the owning tenant's row changed after another tenant's writes");

        await using (var db = CreateContext(tenantB))
        {
            db.Entry(seeded).State = EntityState.Modified;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                throw new TenantIsolationAssertionException("another tenant updated the row");
            }
            catch (TenantIsolationException)
            {
                // Expected.
            }
        }
    }

    private static void Check(bool condition, string failure)
    {
        if (!condition)
        {
            throw new TenantIsolationAssertionException(failure);
        }
    }

    private Task<int> CountAsync<TEntity>(Guid asTenant, Guid ownerTenant, bool ignoreFilters, CancellationToken cancellationToken)
        where TEntity : class, ITenantOwned =>
        InTenantAsync(asTenant, db =>
        {
            var query = ignoreFilters ? db.Set<TEntity>().IgnoreQueryFilters([ShelterDbContext.TenantFilter]) : db.Set<TEntity>();
            return query.CountAsync(e => e.TenantId == ownerTenant, cancellationToken);
        });

    private async Task<int> CountWithoutTenantAsync<TEntity>(Guid ownerTenant, CancellationToken cancellationToken)
        where TEntity : class, ITenantOwned
    {
        await using var db = CreateContext(tenantId: null);
        return await db.Set<TEntity>().IgnoreQueryFilters([ShelterDbContext.TenantFilter])
            .CountAsync(e => e.TenantId == ownerTenant, cancellationToken);
    }

    private Task<int> RawCountInUnitOfWorkAsync(Guid asTenant, string table, Guid ownerTenant, CancellationToken cancellationToken) =>
        InTenantAsync(asTenant, async db =>
        {
            await using var command = InTransaction(db, RawCount((NpgsqlConnection)db.Database.GetDbConnection(), table, ownerTenant));
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        });

    // Returns the affected row count of a raw write in asTenant's unit of work.
    private Task<int> RawInUnitOfWorkAsync(Guid asTenant, string sql, Guid ownerTenant, CancellationToken cancellationToken) =>
        InTenantAsync(asTenant, async db =>
        {
            await using var command = InTransaction(
                db, new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection()) { Parameters = { new("tenant", ownerTenant) } });
            return await command.ExecuteNonQueryAsync(cancellationToken);
        });

    private static NpgsqlCommand InTransaction(ShelterDbContext db, NpgsqlCommand command)
    {
        command.Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
        return command;
    }

    private async Task<int> RawCountWithoutTenantAsync(string table, Guid ownerTenant, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = RawCount(connection, table, ownerTenant);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    // The table name comes from the EF model, never from test input, and SqlIdentifier rejects anything but
    // snake_case before quoting it; the tenant is a bind parameter.
    private static NpgsqlCommand RawCount(NpgsqlConnection connection, string table, Guid ownerTenant) =>
        new($"SELECT count(*) FROM {table} WHERE tenant_id = @tenant", connection) { Parameters = { new("tenant", ownerTenant) } };

    private string TableOf<TEntity>()
    {
        using var db = CreateContext(tenantId: null);
        var entityType = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not in the composed model.");
        return SqlIdentifier.Qualified(entityType.GetSchema() ?? "public", entityType.GetTableName()!);
    }
}

/// <summary>A tenant isolation check failed: data of one tenant was reachable from another, or without a tenant.</summary>
public sealed class TenantIsolationAssertionException : Exception
{
    /// <summary>Creates the exception.</summary>
    public TenantIsolationAssertionException()
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantIsolationAssertionException(string message)
        : base("Tenant isolation violated: " + message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantIsolationAssertionException(string message, Exception innerException)
        : base("Tenant isolation violated: " + message, innerException)
    {
    }
}
