namespace VersionFlow.Extensions.Results;

/// <summary>Wraps existing values in successful outcomes.</summary>
public static class ResultValueExtensions
{
    public static TryResult<T> ToTryResult<T>(this T value) => TryResult<T>.Succeeded(value);

    public static CollectionTryResult<T> ToCollectionTryResult<T>(this IEnumerable<T> values)
        => CollectionTryResult<T>.Succeeded(values);

    public static PagedTryResult<T> ToPagedTryResult<T>(this IEnumerable<T> values, int pageNumber, int pageSize, long totalCount)
        => PagedTryResult<T>.Succeeded(values, pageNumber, pageSize, totalCount);

    public static OffsetTryResult<T> ToOffsetTryResult<T>(this IEnumerable<T> values, long offset, int limit, long totalCount)
        => OffsetTryResult<T>.Succeeded(values, offset, limit, totalCount);

    public static CursorTryResult<T> ToCursorTryResult<T>(this IEnumerable<T> values, string? nextCursor = null)
        => CursorTryResult<T>.Succeeded(values, nextCursor);
}
