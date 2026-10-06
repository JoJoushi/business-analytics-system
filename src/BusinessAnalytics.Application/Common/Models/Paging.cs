namespace BusinessAnalytics.Application.Common.Models;

public record PagedQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    /// <summary>"asc" or "desc".</summary>
    public string? SortDir { get; init; }

    public int Offset => (Math.Max(Page, 1) - 1) * Limit;
    public int Limit => Math.Clamp(PageSize, 1, MaxPageSize);
    public bool Descending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}
