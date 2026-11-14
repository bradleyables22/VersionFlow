namespace VersionFlow.Extensions.Results;

/// <summary>A page with an opaque continuation token. Null means there is no next page.</summary>
public sealed class CursorTryResult<T> : CollectionResult<T>
{
    private CursorTryResult(bool success, IEnumerable<T> data, string? nextCursor = null,
        string? message = null, Exception? exception = null) : base(success, data, message, exception)
        => NextCursor = nextCursor;

    public string? NextCursor { get; }
    public bool HasMore => NextCursor is not null;

    public static CursorTryResult<T> Succeeded(IEnumerable<T> data, string? nextCursor = null)
    {
        if (nextCursor is not null && string.IsNullOrWhiteSpace(nextCursor))
            throw new ArgumentException("A cursor must be nonempty or null.", nameof(nextCursor));
        return new(true, data, nextCursor);
    }

    public static CursorTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
