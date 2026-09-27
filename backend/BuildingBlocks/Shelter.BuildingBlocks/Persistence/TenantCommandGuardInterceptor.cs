using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Rejects any command a tenant-scoped <see cref="ShelterDbContext"/> sends outside a transaction: there,
/// <c>SET LOCAL</c> cannot apply and RLS would silently hide every row. Catches code that bypasses the unit of work.
/// <c>SaveChanges</c> is unaffected: EF wraps it in its own transaction, which the tenant interceptor also covers.
/// </summary>
internal sealed class TenantCommandGuardInterceptor : DbCommandInterceptor
{
    public static TenantCommandGuardInterceptor Instance { get; } = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Guard(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Guard(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Guard(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Guard(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Guard(command, eventData);
        return ValueTask.FromResult(result);
    }

    private static void Guard(DbCommand command, CommandEventData eventData)
    {
        if (eventData.Context is ShelterDbContext { IsPlatformAdmin: false } shelter
            && shelter.TenantContext.TenantId is not null
            && command.Transaction is null)
        {
            throw new TenantTransactionRequiredException();
        }
    }
}
