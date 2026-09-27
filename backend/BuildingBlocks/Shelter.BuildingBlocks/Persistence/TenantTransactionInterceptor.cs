using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shelter.BuildingBlocks.Persistence.Migrations;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Issues <c>SET LOCAL app.tenant_id</c> as the first statement of every transaction a tenant-scoped
/// <see cref="ShelterDbContext"/> starts or joins (ADR 0004). Transaction-scoped: it ends with the transaction, so a
/// pooled connection never carries a tenant into the next unit of work. There is no session-level <c>SET</c>.
/// </summary>
internal sealed class TenantTransactionInterceptor : DbTransactionInterceptor
{
    public static TenantTransactionInterceptor Instance { get; } = new();

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        SetTenant(eventData.Context, result);
        return result;
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(eventData.Context, result, cancellationToken);
        return result;
    }

    public override DbTransaction TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
    {
        SetTenant(eventData.Context, result);
        return result;
    }

    public override async ValueTask<DbTransaction> TransactionUsedAsync(
        DbConnection connection, TransactionEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await SetTenantAsync(eventData.Context, result, cancellationToken);
        return result;
    }

    private static void SetTenant(DbContext? context, DbTransaction transaction)
    {
        using var command = SetTenantCommand(context, transaction);
        command?.ExecuteNonQuery();
    }

    private static async Task SetTenantAsync(DbContext? context, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = SetTenantCommand(context, transaction);
        if (command is not null)
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    // A raw ADO.NET command: it does not pass through the EF command interceptors.
    private static DbCommand? SetTenantCommand(DbContext? context, DbTransaction transaction)
    {
        if (context is not ShelterDbContext { IsPlatformAdmin: false } shelter || shelter.TenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;

        // SET takes no bind parameters. A Guid in "D" format is hex digits and hyphens only, so it cannot inject.
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"SET LOCAL {RlsMigrationExtensions.TenantSetting} = '{tenantId:D}'");
        return command;
    }
}
