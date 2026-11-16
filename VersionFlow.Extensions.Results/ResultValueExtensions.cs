namespace VersionFlow.Extensions.Results;

/// <summary>Wraps existing values in successful outcomes.</summary>
public static class ResultValueExtensions
{
    /// <summary>Wraps this value as a successful outcome, including null.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="value">The value to wrap or transform; reference values are not cloned.</param>
    /// <returns>A successful outcome containing this value.</returns>
    public static TryResult<T> ToTryResult<T>(this T value) => TryResult<T>.Succeeded(value);

    /// <summary>Copies these items into a successful read-only collection.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="values">The items to snapshot; the sequence is enumerated once and item objects are not cloned.</param>
    /// <returns>A successful snapshot of these items.</returns>
    public static CollectionTryResult<T> ToCollectionTryResult<T>(this IEnumerable<T> values)
        => CollectionTryResult<T>.Succeeded(values);

    /// <summary>Wraps an already-fetched page with one-based paging metadata; does not query or slice the items.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="values">The items to snapshot; the sequence is enumerated once and item objects are not cloned.</param>
    /// <param name="pageNumber">The one-based page number; must be at least 1.</param>
    /// <param name="pageSize">The positive maximum number of items per page.</param>
    /// <param name="totalCount">The exact nonnegative total item count across all pages. Returned items must fit the remaining total.</param>
    /// <returns>A successful numbered page with validated metadata.</returns>
    public static PagedTryResult<T> ToPagedTryResult<T>(this IEnumerable<T> values, int pageNumber, int pageSize, long totalCount)
        => PagedTryResult<T>.Succeeded(values, pageNumber, pageSize, totalCount);

    /// <summary>Wraps an already-fetched page with zero-based offset metadata; does not query or slice the items.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="values">The items to snapshot; the sequence is enumerated once and item objects are not cloned.</param>
    /// <param name="offset">The zero-based, nonnegative starting position.</param>
    /// <param name="limit">The positive maximum number of returned items.</param>
    /// <param name="totalCount">The exact nonnegative total item count across all pages. Returned items must fit the remaining total.</param>
    /// <returns>A successful offset page with validated metadata.</returns>
    public static OffsetTryResult<T> ToOffsetTryResult<T>(this IEnumerable<T> values, long offset, int limit, long totalCount)
        => OffsetTryResult<T>.Succeeded(values, offset, limit, totalCount);

    /// <summary>Copies these items into a cursor page with an optional opaque continuation token.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="values">The items to snapshot; the sequence is enumerated once and item objects are not cloned.</param>
    /// <param name="nextCursor">An opaque nonblank continuation token, or null when no next page exists.</param>
    /// <returns>A successful cursor page with the supplied continuation token.</returns>
    public static CursorTryResult<T> ToCursorTryResult<T>(this IEnumerable<T> values, string? nextCursor = null)
        => CursorTryResult<T>.Succeeded(values, nextCursor);
}
