namespace VersionFlow.Extensions.Results;

/// <summary>A page with an opaque continuation token. Null means there is no next page.</summary>
public sealed class CursorTryResult<T> : CollectionResult<T>
{
    private CursorTryResult(bool success, IEnumerable<T> data, string? nextCursor = null,
        string? message = null, Exception? exception = null) : base(success, data, message, exception)
        => NextCursor = nextCursor;

    /// <summary>The opaque continuation token to send on the next request; null ends pagination.</summary>
    public string? NextCursor { get; }
    /// <summary>Whether a continuation token is present; an empty page can still have a next cursor.</summary>
    public bool HasMore => NextCursor is not null;

    /// <summary>Copies the supplied items into a successful result; provide an already-fetched page, not an unsliced query.</summary>
    /// <param name="data">The payload, or already-fetched page items. Collection items are copied once, not queried or sliced.</param>
    /// <param name="nextCursor">An opaque nonblank continuation token, or null when no next page exists.</param>
    /// <returns>A successful immutable outcome containing the supplied data and metadata.</returns>
    public static CursorTryResult<T> Succeeded(IEnumerable<T> data, string? nextCursor = null)
    {
        if (nextCursor is not null && string.IsNullOrWhiteSpace(nextCursor))
            throw new ArgumentException("A cursor must be nonempty or null.", nameof(nextCursor));
        return new(true, data, nextCursor);
    }

    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static CursorTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message: message, exception: exception);
}
