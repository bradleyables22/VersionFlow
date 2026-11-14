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

    public int? PageNumber { get; }
    public int? PageSize { get; }
    public long? TotalCount { get; }
    public long? TotalPages => TotalCount is { } total && PageSize is { } size
        ? total / size + (total % size == 0 ? 0 : 1) : null;
    public bool HasNextPage => Success && PageNumber < TotalPages;
    public bool HasPreviousPage => Success && PageNumber > 1;

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

    public static PagedTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
