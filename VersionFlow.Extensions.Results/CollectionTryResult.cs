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

    public IReadOnlyList<T> Data { get; }
    public int Count => Data.Count;
}

/// <summary>A materialized collection without pagination.</summary>
public sealed class CollectionTryResult<T> : CollectionResult<T>
{
    private CollectionTryResult(bool success, IEnumerable<T> data, string? message = null, Exception? exception = null)
        : base(success, data, message, exception) { }

    public static CollectionTryResult<T> Succeeded(IEnumerable<T> data) => new(true, data);
    public static CollectionTryResult<T> Failed(string message, Exception? exception = null) => new(false, [], message, exception);
}
