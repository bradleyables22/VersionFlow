namespace VersionFlow.Extensions.Railway;

public static partial class RailResultTaskExtensions
{
    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> Ensure<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Ensure(predicate, message, recover, code, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> Ensure<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Ensure(predicate, error, recover, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> EnsureTrue<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureTrue(predicate, message, recover, code, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> EnsureTrue<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureTrue(predicate, error, recover, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="message">A nonblank failure message safe to expose to callers.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> EnsureFalse<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        string message = "Validation failed.",
        Func<T, T>? recover = null,
        RailErrorCode? code = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureFalse(predicate, message, recover, code, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false to pass. An earlier failure skips this check.</param>
    /// <param name="error">The structured error for this validation rule.</param>
    /// <param name="recover">Optional correction applied once after a failed predicate, followed by one recheck.</param>
    /// <param name="onPassed">Observes this check's final successful value and whether this check recovered it. Not called for skipped checks.</param>
    /// <param name="onFailed">Observes this check's final error, including exception or recovery details. Not called for earlier failures.</param>
    /// <returns>A task returning the validated or corrected result, or the unchanged incoming failure.</returns>
    /// <remarks>Exactly one matching observer runs if supplied. Observers do not change the outcome; their exceptions propagate. Earlier failures skip the predicate, recovery, and both observers.</remarks>
    public static async Task<RailResult<T>> EnsureFalse<T>(
        this Task<RailResult<T>> source,
        Func<T, bool> predicate,
        RailError error,
        Func<T, T>? recover = null,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureFalse(predicate, error, recover, onPassed, onFailed);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureAsync(predicate, message, recover, code, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureAsync(predicate, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureTrueAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureTrueAsync(predicate, message, recover, code, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

    /// <summary>Awaits the source, then checks for true, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureTrueAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureTrueAsync(predicate, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

    /// <summary>Awaits the source, then checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureFalseAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.",
        Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureFalseAsync(predicate, message, recover, code, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

    /// <summary>Awaits the source, then checks for false, optionally recovers, and observes only this validation step's final outcome.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
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
    public static async Task<RailResult<T>> EnsureFalseAsync<T>(
        this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate,
        RailError error,
        Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default,
        Action<T, bool>? onPassed = null,
        Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        RailResult<T>.ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureFalseAsync(predicate, error, recover, cancellationToken, onPassed, onFailed, onPassedAsync, onFailedAsync).ConfigureAwait(false);
    }

}
