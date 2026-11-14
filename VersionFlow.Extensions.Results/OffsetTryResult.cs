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

    public long? Offset { get; }
    public int? Limit { get; }
    public long? TotalCount { get; }
    public bool HasMore => Success && Count > 0 && Offset < TotalCount - Count;
    public long? NextOffset => HasMore ? Offset + Count : null;

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

    public static OffsetTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
