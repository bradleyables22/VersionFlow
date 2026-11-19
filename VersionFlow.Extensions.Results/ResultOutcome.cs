using System.Text.Json.Serialization;

namespace VersionFlow.Extensions.Results;

/// <summary>Common outcome metadata. Messages must be safe to expose to callers.</summary>
public abstract class ResultOutcome
{
    private protected ResultOutcome(bool success, string? message = null, Exception? exception = null)
    {
        if (!success && string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A failure requires a message.", nameof(message));
        IsSuccess = success;
        Message = message;
        Exception = exception;
    }

    /// <summary>Whether the operation succeeded. Check before using the payload or page metadata.</summary>
    public bool IsSuccess { get; }
    /// <summary>The caller-safe failure message, or null on success.</summary>
    public string? Message { get; }

    /// <summary>Diagnostic information for logging; never serialized by System.Text.Json.</summary>
    [JsonIgnore]
    public Exception? Exception { get; }
}
