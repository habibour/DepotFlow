namespace DepotFlow.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Page is at least 1; page size defaults to 20 and is capped at <paramref name="maxPageSize"/> (100 unless stated).</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize, int maxPageSize = MaxPageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, maxPageSize));
}
