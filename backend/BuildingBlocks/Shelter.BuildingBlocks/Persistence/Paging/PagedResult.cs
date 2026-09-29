namespace Shelter.BuildingBlocks.Persistence.Paging;

/// <summary>One page of <typeparamref name="T"/> with the total row count, so clients can render page controls.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
