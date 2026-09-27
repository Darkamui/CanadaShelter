namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Runs work inside one explicit transaction on the scoped <see cref="ShelterDbContext"/>, so the tenant set by
/// <c>SET LOCAL</c> covers every statement (ADR 0004). Nested calls join the outer transaction. Scoped.
/// </summary>
public sealed class UnitOfWork(ShelterDbContext db)
{
    /// <summary>Runs <paramref name="work"/>; commits when it completes, rolls back when it throws.</summary>
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return ExecuteAsync(
            async ct =>
            {
                await work(ct);
                return true;
            },
            static _ => true,
            cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="work"/>; commits when <paramref name="shouldCommit"/> accepts its result, rolls back
    /// otherwise or when it throws.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> shouldCommit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(shouldCommit);

        if (db.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        // Disposing an uncommitted transaction rolls it back, including when work throws.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        if (shouldCommit(result))
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        return result;
    }
}
