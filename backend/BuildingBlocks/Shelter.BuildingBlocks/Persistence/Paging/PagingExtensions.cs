using System.Linq.Expressions;
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

    /// <summary>
    /// Like <see cref="ToPagedResultAsync{T}(IOrderedQueryable{T}, PageRequest, CancellationToken)"/>, projecting each
    /// row of the page with <paramref name="selector"/>: order by entity columns, return a DTO.
    /// </summary>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IOrderedQueryable<TSource> query,
        Expression<Func<TSource, TResult>> selector,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(page);

        var total = await query.CountAsync(cancellationToken);
        var items = total == 0
            ? []
            : await query.Skip(page.Skip).Take(page.Size).Select(selector).ToListAsync(cancellationToken);
        return new PagedResult<TResult>(items, page.CurrentPage, page.Size, total);
    }
}
