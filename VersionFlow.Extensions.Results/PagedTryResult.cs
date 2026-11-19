namespace VersionFlow.Extensions.Results;

/// <summary>A one-based numbered page with an exact total count.</summary>
public sealed class PagedTryResult<T> : CollectionResult<T>
{
    private PagedTryResult(bool success, IEnumerable<T> data, int? pageNumber = null, int? pageSize = null,
        long? totalCount = null, string? message = null, Exception? exception = null)
        : base(success, data, message, exception)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>The one-based page number, or null on failure.</summary>
    public int? PageNumber { get; }
    /// <summary>The requested positive page size, or null on failure.</summary>
    public int? PageSize { get; }
    /// <summary>The exact nonnegative count across all pages, or null on failure.</summary>
    public long? TotalCount { get; }
    /// <summary>The number of pages required for TotalCount; zero for no items, null on failure.</summary>
    public long? TotalPages => TotalCount is { } total && PageSize is { } size
        ? total / size + (total % size == 0 ? 0 : 1) : null;
    /// <summary>Whether a successful numbered page has a later page according to TotalCount.</summary>
    public bool HasNextPage => IsSuccess && PageNumber < TotalPages;
    /// <summary>Whether a successful numbered page has a page number greater than one.</summary>
    public bool HasPreviousPage => IsSuccess && PageNumber > 1;

    /// <summary>Copies the supplied items into a successful result; provide an already-fetched page, not an unsliced query.</summary>
    /// <param name="data">The payload, or already-fetched page items. Collection items are copied once, not queried or sliced.</param>
    /// <param name="pageNumber">The one-based page number; must be at least 1.</param>
    /// <param name="pageSize">The positive maximum number of items per page.</param>
    /// <param name="totalCount">The exact nonnegative total item count across all pages. Returned items must fit the remaining total.</param>
    /// <returns>A successful immutable outcome containing the supplied data and metadata.</returns>
    public static PagedTryResult<T> Succeeded(IEnumerable<T> data, int pageNumber, int pageSize, long totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        var result = new PagedTryResult<T>(true, data, pageNumber, pageSize, totalCount);
        var offset = ((long)pageNumber - 1) * pageSize;
        if (result.Count > pageSize || result.Count > Math.Max(0, totalCount - offset))
            throw new ArgumentException("Items exceed the page size or remaining total.", nameof(data));
        return result;
    }

    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static PagedTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
