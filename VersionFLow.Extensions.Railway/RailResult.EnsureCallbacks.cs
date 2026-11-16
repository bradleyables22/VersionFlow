namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> Ensure(
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, true, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, onPassed, onFailed);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> Ensure(
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, true, error, recover, onPassed, onFailed);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> EnsureTrue(
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, true, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, onPassed, onFailed);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> EnsureTrue(
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, true, error, recover, onPassed, onFailed);

    /// <summary>Checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> EnsureFalse(
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, false, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, onPassed, onFailed);

    /// <summary>Checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>The validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public RailResult<T> EnsureFalse(
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null) =>
        EnsureCore(predicate, false, error, recover, onPassed, onFailed);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, true, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, true, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureTrueAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, true, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

    /// <summary>Checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureTrueAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, true, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

    /// <summary>Checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureFalseAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, false, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

    /// <summary>Checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="cancellationToken">Passed to asynchronous callbacks. Cancellation propagates and does not produce a validation failure.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <param name="onPassedAsync">Asynchronously observes the final value and recovery status. Use either this or onPassed, not both.</param>
    /// <param name="onFailedAsync">Asynchronously observes the final error. Use either this or onFailed, not both.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    /// <exception cref="ArgumentException">Both synchronous and asynchronous observers are supplied for the same outcome.</exception>
    public Task<RailResult<T>> EnsureFalseAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null) =>
        EnsureCoreAsync(predicate, false, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync);

}
