namespace VersionFlow.Extensions.Railway;

/// <summary>An extensible, case-sensitive error identifier. The default value means unspecified.</summary>
public readonly record struct RailErrorCode
{
    private readonly string? _value;

    /// <summary>Creates a case-sensitive application error code; null, empty, and whitespace identifiers are rejected.</summary>
    /// <param name="value">A nonblank case-sensitive identifier; unspecified maps to the default code.</param>
    public RailErrorCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value == "unspecified" ? null : value;
    }

    /// <summary>The stable error identifier; default values return unspecified.</summary>
    public string Value => _value ?? "unspecified";
    /// <summary>Returns the stable error identifier, including unspecified for a default code.</summary>
    public override string ToString() => Value;
}

/// <summary>Built-in codes; applications can define their own RailErrorCode constants.</summary>
public static class RailErrorCodes
{
    /// <summary>No specific application error code was supplied.</summary>
    public static readonly RailErrorCode Unspecified = default;
    /// <summary>A validation predicate did not meet its required condition.</summary>
    public static readonly RailErrorCode ValidationFailed = new("validation_failed");
    /// <summary>An operation threw an exception that was captured as a failure.</summary>
    public static readonly RailErrorCode Exception = new("exception");
    /// <summary>A recovery attempt threw or failed its validation recheck.</summary>
    public static readonly RailErrorCode RecoveryFailed = new("recovery_failed");
}

/// <summary>Details of a correction that threw or did not satisfy its validation rule.</summary>
public readonly record struct RailRecoveryFailure
{
    /// <summary>Records a failed correction with a nonblank message and optional exception.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">The exception retained for diagnostics.</param>
    public RailRecoveryFailure(string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Exception = exception;
    }

    /// <summary>The built-in recovery_failed code.</summary>
    public RailErrorCode Code => RailErrorCodes.RecoveryFailed;
    /// <summary>The failure description supplied to the constructor. A default struct has no message.</summary>
    public string Message { get; }
    /// <summary>The underlying exception when available; use it for diagnostics.</summary>
    public Exception? Exception { get; }
}

/// <summary>A failure with an optional exception and recovery details.</summary>
public readonly record struct RailError
{
    /// <summary>Creates structured failure details with a nonblank message and optional exception or recovery information.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="code">An optional application error identifier.</param>
    /// <param name="exception">The exception retained for diagnostics.</param>
    /// <param name="recoveryFailure">Optional details of a correction that failed or threw.</param>
    public RailError(string message, RailErrorCode code = default, Exception? exception = null,
        RailRecoveryFailure? recoveryFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
        Code = code;
        Exception = exception;
        RecoveryFailure = recoveryFailure;
    }

    /// <summary>The application error identifier; default means unspecified.</summary>
    public RailErrorCode Code { get; }
    /// <summary>The failure description supplied to the constructor. A default struct has no message.</summary>
    public string Message { get; }
    /// <summary>The underlying exception when available; use it for diagnostics.</summary>
    public Exception? Exception { get; }
    /// <summary>Details of an unsuccessful recovery attempt, or null when none occurred.</summary>
    public RailRecoveryFailure? RecoveryFailure { get; }

    internal RailError WithRecovery(RailRecoveryFailure failure) => new(Message, Code, Exception, failure);
}

/// <summary>A value for successful operations that have no response payload.</summary>
public readonly record struct RailUnit
{
    /// <summary>The empty success value for operations without a payload.</summary>
    public static RailUnit Value => default;
}
