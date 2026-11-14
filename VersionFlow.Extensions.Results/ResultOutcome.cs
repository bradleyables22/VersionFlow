using System.Text.Json.Serialization;

namespace VersionFlow.Extensions.Results;

/// <summary>Common outcome metadata. Messages must be safe to expose to callers.</summary>
public abstract class ResultOutcome
{
    private protected ResultOutcome(bool success, string? message = null, Exception? exception = null)
    {
        if (!success && string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A failure requires a message.", nameof(message));
        Success = success;
        Message = message;
        Exception = exception;
    }

    public bool Success { get; }
    public string? Message { get; }

    /// <summary>Diagnostic information for logging; never serialized by System.Text.Json.</summary>
    [JsonIgnore]
    public Exception? Exception { get; }
}
