namespace VersionFlow.Extensions.Results;

/// <summary>Items are copied once; item objects are not cloned.</summary>
public abstract class CollectionResult<T> : ResultOutcome
{
    private protected CollectionResult(bool success, IEnumerable<T> data, string? message = null, Exception? exception = null)
        : base(success, message, exception)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = Array.AsReadOnly(data.ToArray());
    }

    /// <summary>The read-only item snapshot. Empty on failure; individual item objects are not cloned.</summary>
    public IReadOnlyList<T> Data { get; }
    /// <summary>The number of items in this result, not the total across all pages.</summary>
    public int Count => Data.Count;
}

/// <summary>A materialized collection without pagination.</summary>
public sealed class CollectionTryResult<T> : CollectionResult<T>
{
    private CollectionTryResult(bool success, IEnumerable<T> data, string? message = null, Exception? exception = null)
        : base(success, data, message, exception) { }

    /// <summary>Materializes the supplied items once into a read-only successful collection.</summary>
    /// <param name="data">The payload, or already-fetched page items. Collection items are copied once, not queried or sliced.</param>
    /// <returns>A successful immutable outcome containing the supplied data and metadata.</returns>
    public static CollectionTryResult<T> Succeeded(IEnumerable<T> data) => new(true, data);
    /// <summary>Creates a failed outcome with a caller-safe message and optional diagnostic exception; no payload is retained.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">Optional diagnostic exception, excluded from System.Text.Json output.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static CollectionTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message, exception);
}
