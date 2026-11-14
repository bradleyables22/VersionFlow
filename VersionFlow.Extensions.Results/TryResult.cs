namespace VersionFlow.Extensions.Results;

/// <summary>An operation outcome without a payload.</summary>
public sealed class TryResult : ResultOutcome
{
    private TryResult(bool success, string? message = null, Exception? exception = null)
        : base(success, message, exception) { }

    public static TryResult Succeeded() => new(true);
    public static TryResult Failed(string message, Exception? exception = null) => new(false, message, exception);
}

/// <summary>An operation outcome with a payload. A successful null value is allowed.</summary>
public sealed class TryResult<T> : ResultOutcome
{
    private TryResult(bool success, T? data, string? message = null, Exception? exception = null)
        : base(success, message, exception) => Data = data;

    /// <summary>Check Success before using Data; failures contain default(T).</summary>
    public T? Data { get; }

    public static TryResult<T> Succeeded(T data) => new(true, data);
    public static TryResult<T> Failed(string message, Exception? exception = null) => new(false, default, message, exception);
}
