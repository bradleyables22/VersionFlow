namespace VersionFlow.Extensions.Railway;

/// <summary>An extensible, case-sensitive error identifier. The default value means unspecified.</summary>
public readonly record struct RailErrorCode
{
    private readonly string? _value;

    public RailErrorCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value == "unspecified" ? null : value;
    }

    public string Value => _value ?? "unspecified";
    public override string ToString() => Value;
}

/// <summary>Built-in codes; applications can define their own RailErrorCode constants.</summary>
public static class RailErrorCodes
{
    public static readonly RailErrorCode Unspecified = default;
    public static readonly RailErrorCode ValidationFailed = new("validation_failed");
    public static readonly RailErrorCode Exception = new("exception");
    public static readonly RailErrorCode RecoveryFailed = new("recovery_failed");
}

/// <summary>Details of a correction that threw or did not satisfy its validation rule.</summary>
public readonly record struct RailRecoveryFailure
{
    public RailRecoveryFailure(string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Exception = exception;
    }

    public RailErrorCode Code => RailErrorCodes.RecoveryFailed;
    public string Message { get; }
    public Exception? Exception { get; }
}

/// <summary>A failure with an optional exception and recovery details.</summary>
public readonly record struct RailError
{
    public RailError(string message, RailErrorCode code = default, Exception? exception = null,
        RailRecoveryFailure? recoveryFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Code = code;
        Exception = exception;
        RecoveryFailure = recoveryFailure;
    }

    public RailErrorCode Code { get; }
    public string Message { get; }
    public Exception? Exception { get; }
    public RailRecoveryFailure? RecoveryFailure { get; }

    internal RailError WithRecovery(RailRecoveryFailure failure) => new(Message, Code, Exception, failure);
}

/// <summary>A value for successful operations that have no response payload.</summary>
public readonly record struct RailUnit
{
    public static RailUnit Value => default;
}
