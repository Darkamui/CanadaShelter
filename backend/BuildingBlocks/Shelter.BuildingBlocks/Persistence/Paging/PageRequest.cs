namespace Shelter.BuildingBlocks.Persistence.Paging;

/// <summary>
/// Offset paging from the query string (<c>?page=2&amp;pageSize=50</c>), bound with <c>[AsParameters]</c>.
/// Out-of-range values are clamped rather than rejected: page ≥ 1, page size 1–<see cref="MaxPageSize"/>.
/// </summary>
public sealed record PageRequest(int? Page = null, int? PageSize = null)
{
    /// <summary>Page size when none is given.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Largest page size a client may ask for.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The 1-based page, clamped.</summary>
    public int CurrentPage => Math.Max(Page ?? 1, 1);

    /// <summary>The page size, clamped.</summary>
    public int Size => Math.Clamp(PageSize ?? DefaultPageSize, 1, MaxPageSize);

    /// <summary>Rows to skip. Pages far past the end simply return no items.</summary>
    public int Skip => (int)Math.Min((long)(CurrentPage - 1) * Size, int.MaxValue);
}
