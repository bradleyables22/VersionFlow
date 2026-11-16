namespace VersionFlow.Extensions.Railway;

/// <summary>Composes synchronous and asynchronous steps after a Task of a railway result.</summary>
/// <remarks>Upstream task faults propagate. Cancellation stops waiting but does not cancel upstream work.</remarks>
public static class RailResultTaskExtensions
{
    /// <summary>Awaits the source and delegates to Ensure, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> Ensure<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, string message = "Validation failed.", Func<T, T>? recover = null, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Ensure(predicate, message, recover, code);
    }

    /// <summary>Awaits the source and delegates to Ensure, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> Ensure<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Ensure(predicate, error, recover);
    }

    /// <summary>Awaits the source and delegates to EnsureAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null, RailErrorCode? code = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureAsync(predicate, message, recover, code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to EnsureAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, RailError error, Func<T, CancellationToken, Task<T>>? recover = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureAsync(predicate, error, recover, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to EnsureTrue, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureTrue<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, string message = "Validation failed.", Func<T, T>? recover = null, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureTrue(predicate, message, recover, code);
    }

    /// <summary>Awaits the source and delegates to EnsureTrue, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureTrue<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureTrue(predicate, error, recover);
    }

    /// <summary>Awaits the source and delegates to EnsureTrueAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureTrueAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null, RailErrorCode? code = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureTrueAsync(predicate, message, recover, code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to EnsureTrueAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureTrueAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, RailError error, Func<T, CancellationToken, Task<T>>? recover = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureTrueAsync(predicate, error, recover, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to EnsureFalse, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureFalse<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, string message = "Validation failed.", Func<T, T>? recover = null, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureFalse(predicate, message, recover, code);
    }

    /// <summary>Awaits the source and delegates to EnsureFalse, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureFalse<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.EnsureFalse(predicate, error, recover);
    }

    /// <summary>Awaits the source and delegates to EnsureFalseAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureFalseAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null, RailErrorCode? code = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureFalseAsync(predicate, message, recover, code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to EnsureFalseAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public static async Task<RailResult<T>> EnsureFalseAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, RailError error, Func<T, CancellationToken, Task<T>>? recover = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.EnsureFalseAsync(predicate, error, recover, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to Select, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="selector">Transforms a successful value into the next payload.</param>
    /// <returns>A task returning a result with the transformed payload, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> Select<T, TNext>(this Task<RailResult<T>> source,
        Func<T, TNext> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Select(selector);
    }

    /// <summary>Awaits the source and delegates to SelectAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="selector">Transforms a successful value into the next payload.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning a result with the transformed payload, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> SelectAsync<T, TNext>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<TNext>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.SelectAsync(selector, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to Then, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <returns>A task returning the callback result on success, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> Then<T, TNext>(this Task<RailResult<T>> source,
        Func<T, RailResult<TNext>> operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Then(operation);
    }

    /// <summary>Awaits the source and delegates to ThenAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result on success, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> ThenAsync<T, TNext>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<RailResult<TNext>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.ThenAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to Recover, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="recovery">Produces a replacement result from the existing errors; skipped on success.</param>
    /// <returns>A task returning the original success or the replacement result. Recovery exceptions append to the original errors.</returns>
    public static async Task<RailResult<T>> Recover<T>(this Task<RailResult<T>> source,
        Func<IReadOnlyList<RailError>, RailResult<T>> recovery)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recovery);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Recover(recovery);
    }

    /// <summary>Awaits the source and delegates to RecoverAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="recovery">Produces a replacement result from the existing errors; skipped on success.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the original success or the replacement result. Recovery exceptions append to the original errors.</returns>
    public static async Task<RailResult<T>> RecoverAsync<T>(this Task<RailResult<T>> source,
        Func<IReadOnlyList<RailError>, CancellationToken, Task<RailResult<T>>> recovery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recovery);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.RecoverAsync(recovery, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to MapErrors, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="mapper">Transforms each existing error in input order.</param>
    /// <returns>A task returning the original success or a failure with mapped errors. Mapping exceptions preserve the original errors and append the exception.</returns>
    public static async Task<RailResult<T>> MapErrors<T>(this Task<RailResult<T>> source,
        Func<RailError, RailError> mapper)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.MapErrors(mapper);
    }

    /// <summary>Awaits the source and delegates to MapErrorsAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="mapper">Transforms each existing error in input order.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the original success or a failure with mapped errors. Mapping exceptions preserve the original errors and append the exception.</returns>
    public static async Task<RailResult<T>> MapErrorsAsync<T>(this Task<RailResult<T>> source,
        Func<RailError, CancellationToken, Task<RailError>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.MapErrorsAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to OnSuccess, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <returns>A task returning the unchanged outcome after observing success. Observer exceptions propagate.</returns>
    public static async Task<RailResult<T>> OnSuccess<T>(this Task<RailResult<T>> source,
        Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.OnSuccess(action);
    }

    /// <summary>Awaits the source and delegates to OnSuccessAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the unchanged outcome after observing success. Observer exceptions propagate.</returns>
    public static async Task<RailResult<T>> OnSuccessAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.OnSuccessAsync(action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to OnFailure, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <returns>A task returning the unchanged outcome after observing failure. Observer exceptions propagate.</returns>
    public static async Task<RailResult<T>> OnFailure<T>(this Task<RailResult<T>> source,
        Action<IReadOnlyList<RailError>> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.OnFailure(action);
    }

    /// <summary>Awaits the source and delegates to OnFailureAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the unchanged outcome after observing failure. Observer exceptions propagate.</returns>
    public static async Task<RailResult<T>> OnFailureAsync<T>(this Task<RailResult<T>> source,
        Func<IReadOnlyList<RailError>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.OnFailureAsync(action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to When, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <returns>A task returning the callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public static async Task<RailResult<T>> When<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, Func<T, RailResult<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.When(predicate, operation);
    }

    /// <summary>Awaits the source and delegates to WhenAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public static async Task<RailResult<T>> WhenAsync<T>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, Func<T, CancellationToken, Task<RailResult<T>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.WhenAsync(predicate, operation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to WhenAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public static async Task<RailResult<T>> WhenAsync<T>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, Func<T, CancellationToken, Task<RailResult<T>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.WhenAsync(predicate, operation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to Branch, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <returns>A task returning the result of exactly one selected path, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> Branch<T, TNext>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, Func<T, RailResult<TNext>> whenTrue, Func<T, RailResult<TNext>> whenFalse)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Branch(predicate, whenTrue, whenFalse);
    }

    /// <summary>Awaits the source and delegates to BranchAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the result of exactly one selected path, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> BranchAsync<T, TNext>(this Task<RailResult<T>> source,
        Func<T, bool> predicate, Func<T, CancellationToken, Task<RailResult<TNext>>> whenTrue, Func<T, CancellationToken, Task<RailResult<TNext>>> whenFalse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.BranchAsync(predicate, whenTrue, whenFalse, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to BranchAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the result of exactly one selected path, or the existing errors when the input failed.</returns>
    public static async Task<RailResult<TNext>> BranchAsync<T, TNext>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<bool>> predicate, Func<T, CancellationToken, Task<RailResult<TNext>>> whenTrue, Func<T, CancellationToken, Task<RailResult<TNext>>> whenFalse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.BranchAsync(predicate, whenTrue, whenFalse, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and delegates to Match, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TOutput">The value returned by either outcome callback.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="passed">The callback used only when the result is successful.</param>
    /// <param name="failed">The callback used only when the result has failed.</param>
    /// <returns>A task returning the value produced by the callback matching the outcome. Callback exceptions propagate.</returns>
    public static async Task<TOutput> Match<T, TOutput>(this Task<RailResult<T>> source,
        Func<T, TOutput> passed, Func<IReadOnlyList<RailError>, TOutput> failed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(passed);
        ArgumentNullException.ThrowIfNull(failed);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.Match(passed, failed);
    }

    /// <summary>Awaits the source and delegates to MatchAsync, preserving its outcome semantics.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TOutput">The value returned by either outcome callback.</typeparam>
    /// <param name="source">The upstream result task. Faults propagate; cancelling the wait does not cancel upstream work.</param>
    /// <param name="passed">The callback used only when the result is successful.</param>
    /// <param name="failed">The callback used only when the result has failed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the value produced by the callback matching the outcome. Callback exceptions propagate.</returns>
    public static async Task<TOutput> MatchAsync<T, TOutput>(this Task<RailResult<T>> source,
        Func<T, CancellationToken, Task<TOutput>> passed, Func<IReadOnlyList<RailError>, CancellationToken, Task<TOutput>> failed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(passed);
        ArgumentNullException.ThrowIfNull(failed);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.MatchAsync(passed, failed, cancellationToken).ConfigureAwait(false);
    }

}
