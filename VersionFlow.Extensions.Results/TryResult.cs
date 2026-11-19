namespace VersionFlow.Extensions.Results;

/// <summary>An operation outcome without a payload.</summary>
public sealed class TryResult : ResultOutcome
{
    private TryResult(bool success, string? message = null, Exception? exception = null)
        : base(success, message, exception) { }

    /// <summary>Creates a successful command outcome without a payload.</summary>
    /// <returns>A successful outcome without a payload.</returns>
    public static TryResult Succeeded() => new(true);
    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static TryResult Failed(string message, Exception? exception = null) => new(false, message, exception);
}

/// <summary>An operation outcome with a payload. A successful null value is allowed.</summary>
public sealed class TryResult<T> : ResultOutcome
{
    private TryResult(bool success, T? data, string? message = null, Exception? exception = null)
        : base(success, message, exception) => Data = data;

    /// <summary>Check IsSuccess before using Data; failures contain default(T).</summary>
    public T? Data { get; }

    /// <summary>Creates a successful outcome from the supplied payload; null is allowed.</summary>
    /// <param name="data">The payload, or already-fetched page items. Collection items are copied once, not queried or sliced.</param>
    /// <returns>A successful immutable outcome containing the supplied data and metadata.</returns>
    public static TryResult<T> Succeeded(T data) => new(true, data);
    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static TryResult<T> Failed(string message, Exception? exception = null) => new(false, default, message, exception);
}
