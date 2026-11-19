namespace VersionFlow.Extensions.Results;

/// <summary>A zero-based offset page with an exact total count.</summary>
public sealed class OffsetTryResult<T> : CollectionResult<T>
{
    private OffsetTryResult(bool success, IEnumerable<T> data, long? offset = null, int? limit = null,
        long? totalCount = null, string? message = null, Exception? exception = null)
        : base(success, data, message, exception)
    {
        Offset = offset;
        Limit = limit;
        TotalCount = totalCount;
    }

    /// <summary>The zero-based starting position, or null on failure.</summary>
    public long? Offset { get; }
    /// <summary>The requested positive maximum item count, or null on failure.</summary>
    public int? Limit { get; }
    /// <summary>The exact nonnegative count across all pages, or null on failure.</summary>
    public long? TotalCount { get; }
    /// <summary>Whether this successful, nonempty page leaves items after its last returned item.</summary>
    public bool HasMore => IsSuccess && Count > 0 && Offset < TotalCount - Count;
    /// <summary>The current offset plus returned item count when more items remain; otherwise null.</summary>
    public long? NextOffset => HasMore ? Offset + Count : null;

    /// <summary>Copies the supplied items into a successful result; provide an already-fetched page, not an unsliced query.</summary>
    /// <param name="data">The payload, or already-fetched page items. Collection items are copied once, not queried or sliced.</param>
    /// <param name="offset">The zero-based, nonnegative starting position.</param>
    /// <param name="limit">The positive maximum number of returned items.</param>
    /// <param name="totalCount">The exact nonnegative total item count across all pages. Returned items must fit the remaining total.</param>
    /// <returns>A successful immutable outcome containing the supplied data and metadata.</returns>
    public static OffsetTryResult<T> Succeeded(IEnumerable<T> data, long offset, int limit, long totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        var result = new OffsetTryResult<T>(true, data, offset, limit, totalCount);
        if (result.Count > limit || result.Count > Math.Max(0, totalCount - offset))
            throw new ArgumentException("Items exceed the limit or remaining total.", nameof(data));
        return result;
    }

    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static OffsetTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
