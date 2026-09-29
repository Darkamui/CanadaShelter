using Microsoft.EntityFrameworkCore;

namespace Shelter.BuildingBlocks.Persistence.Paging;

/// <summary>Applies a <see cref="PageRequest"/> to an EF query.</summary>
public static class PagingExtensions
{
    /// <summary>
    /// Counts <paramref name="query"/>, then reads the requested page. The query must already be ordered, ending with
    /// a unique key (the <c>Id</c>) so pages are stable.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IOrderedQueryable<T> query, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        var total = await query.CountAsync(cancellationToken);
        var items = total == 0
            ? []
            : await query.Skip(page.Skip).Take(page.Size).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page.CurrentPage, page.Size, total);
    }
}
