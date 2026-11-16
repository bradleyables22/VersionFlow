namespace VersionFlow.Extensions.Railway;

/// <summary>An immutable success value or a nonempty, read-only collection of errors.</summary>
/// <remarks>Values are not cloned. Prefer immutable values when applying recovery.</remarks>
public sealed partial class RailResult<T>
{
    private readonly T? _value;

    private RailResult(T value)
    {
        _value = value;
        IsPassed = true;
        Errors = Array.Empty<RailError>();
    }

    private RailResult(IReadOnlyList<RailError> errors) => Errors = errors;

    /// <summary>Whether the result contains a successful value.</summary>
    public bool IsPassed { get; }
    /// <summary>Whether the result contains errors instead of a value.</summary>
    public bool IsFailed => !IsPassed;
    /// <summary>Read-only failure details in order; empty on success.</summary>
    public IReadOnlyList<RailError> Errors { get; }
    /// <summary>The successful value. Throws InvalidOperationException when the result has failed.</summary>
    public T Value => IsPassed ? _value! : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Wraps a value in a successful result without cloning or validating it; null is allowed.</summary>
    /// <param name="value">The value to wrap or transform; reference values are not cloned.</param>
    /// <returns>A successful result containing the supplied value.</returns>
    public static RailResult<T> Passed(T value) => new(value);

    /// <summary>Creates a failed result with a nonblank message and an optional application error code.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="code">An optional application error identifier.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static RailResult<T> Failed(string message, RailErrorCode code = default) =>
        Failed(new RailError(message, code));

    /// <summary>Creates a failed result, retaining the exception for diagnostics.</summary>
    /// <param name="exception">The exception retained for diagnostics.</param>
    /// <param name="code">An optional application error identifier.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static RailResult<T> Failed(Exception exception, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failed(new RailError(
            string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message,
            code ?? RailErrorCodes.Exception, exception));
    }

    /// <summary>Creates a failed result, retaining the exception for diagnostics.</summary>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="exception">The exception retained for diagnostics.</param>
    /// <param name="code">An optional application error identifier.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static RailResult<T> Failed(string message, Exception exception, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failed(new RailError(message, code ?? RailErrorCodes.Exception, exception));
    }

    /// <summary>Creates a failed result containing the supplied error.</summary>
    /// <param name="error">The structured failure to store.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static RailResult<T> Failed(RailError error) => Failed(new[] { error });

    /// <summary>Creates a failed result from a nonempty snapshot of the supplied errors.</summary>
    /// <param name="errors">A nonempty sequence of failure details; copied into a read-only snapshot.</param>
    /// <returns>A failed result containing the supplied failure details.</returns>
    public static RailResult<T> Failed(IEnumerable<RailError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var copy = errors.ToArray();
        if (copy.Length == 0)
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));
        if (copy.Any(e => string.IsNullOrWhiteSpace(e.Message) ||
            (e.RecoveryFailure is { } recovery && string.IsNullOrWhiteSpace(recovery.Message))))
            throw new ArgumentException("Errors and recovery details must have messages; default structs are invalid.", nameof(errors));
        return new RailResult<T>(Array.AsReadOnly(copy));
    }

    /// <summary>Compatibility alias for EnsureTrue; the predicate must return true to pass.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> Ensure(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureTrue(predicate, message, recover, code);

    /// <summary>Compatibility alias for EnsureTrue; the predicate must return true to pass.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> Ensure(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureTrue(predicate, error, recover);

    /// <summary>Requires true, optionally correcting the value once and rechecking for true.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> EnsureTrue(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureTrue(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover);

    /// <summary>Requires true, optionally correcting the value once and rechecking for true.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> EnsureTrue(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureCore(predicate, true, error, recover);

    /// <summary>Requires false, optionally correcting the value once and rechecking for false.</summary>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> EnsureFalse(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureFalse(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover);

    /// <summary>Requires false, optionally correcting the value once and rechecking for false.</summary>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <returns>The validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public RailResult<T> EnsureFalse(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureCore(predicate, false, error, recover);

    /// <summary>Requires true asynchronously, optionally correcting once and rechecking.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureAsync(Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null, CancellationToken cancellationToken = default) =>
        EnsureAsync(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken);

    /// <summary>Requires true asynchronously with a structured validation error.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureAsync(Func<T, CancellationToken, Task<bool>> predicate,
        RailError error, Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default) =>
        EnsureCoreAsync(predicate, true, error, recover, cancellationToken);

    /// <summary>Requires true asynchronously, optionally correcting once and rechecking.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureTrueAsync(Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null, CancellationToken cancellationToken = default) =>
        EnsureTrueAsync(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken);

    /// <summary>Requires true asynchronously with a structured validation error.</summary>
    /// <param name="predicate">Must return true for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureTrueAsync(Func<T, CancellationToken, Task<bool>> predicate,
        RailError error, Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default) =>
        EnsureCoreAsync(predicate, true, error, recover, cancellationToken);

    /// <summary>Requires false asynchronously, optionally correcting once and rechecking.</summary>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="code">The validation error code; null uses RailErrorCodes.ValidationFailed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureFalseAsync(Func<T, CancellationToken, Task<bool>> predicate,
        string message = "Validation failed.", Func<T, CancellationToken, Task<T>>? recover = null,
        RailErrorCode? code = null, CancellationToken cancellationToken = default) =>
        EnsureFalseAsync(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover, cancellationToken);

    /// <summary>Requires false asynchronously with a structured validation error.</summary>
    /// <param name="predicate">Must return false for validation to pass; a failed input skips the check.</param>
    /// <param name="error">The structured error to return if validation fails.</param>
    /// <param name="recover">An optional correction applied once when validation fails; the corrected value is checked again.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the validated or successfully corrected result; otherwise a failure. Existing failures are unchanged.</returns>
    public Task<RailResult<T>> EnsureFalseAsync(Func<T, CancellationToken, Task<bool>> predicate,
        RailError error, Func<T, CancellationToken, Task<T>>? recover = null,
        CancellationToken cancellationToken = default) =>
        EnsureCoreAsync(predicate, false, error, recover, cancellationToken);

    private async Task<RailResult<T>> EnsureCoreAsync(Func<T, CancellationToken, Task<bool>> predicate,
        bool expected, RailError error, Func<T, CancellationToken, Task<T>>? recover,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var failure = Failed(error);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) return this;
        try
        {
            if (await predicate(Value, cancellationToken).ConfigureAwait(false) == expected) return this;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(new RailError(error.Message, error.Code, exception));
        }

        if (recover is null) return failure;
        try
        {
            var corrected = await recover(Value, cancellationToken).ConfigureAwait(false);
            return await predicate(corrected, cancellationToken).ConfigureAwait(false) == expected
                ? Passed(corrected)
                : Failed(error.WithRecovery(new RailRecoveryFailure("The corrected value did not satisfy the condition.")));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(error.WithRecovery(new RailRecoveryFailure("Recovery or its validation threw an exception.", exception)));
        }
    }

    private RailResult<T> EnsureCore(Func<T, bool> predicate, bool expected, RailError error, Func<T, T>? recover)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        // Validate even when this step is skipped, like other invalid API arguments.
        var failure = Failed(error);
        if (IsFailed) return this;
        try
        {
            if (predicate(Value) == expected) return this;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(new RailError(error.Message, error.Code, exception));
        }

        if (recover is null) return failure;
        try
        {
            var corrected = recover(Value);
            return predicate(corrected) == expected
                ? Passed(corrected)
                : Failed(error.WithRecovery(new RailRecoveryFailure("The corrected value did not satisfy the condition.")));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(error.WithRecovery(new RailRecoveryFailure("Recovery or its validation threw an exception.", exception)));
        }
    }

    /// <summary>Transforms a successful value; captures callback exceptions except cancellation.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="selector">Transforms a successful value into the next payload.</param>
    /// <returns>A result with the transformed payload, or the existing errors when the input failed.</returns>
    public RailResult<TNext> Select<TNext>(Func<T, TNext> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try { return RailResult<TNext>.Passed(selector(Value)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

    /// <summary>Chains an operation that returns its own result.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <returns>The callback result on success, or the existing errors when the input failed.</returns>
    public RailResult<TNext> Then<TNext>(Func<T, RailResult<TNext>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try { return operation(Value) ?? throw new InvalidOperationException("The operation returned a null result."); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

    /// <summary>Chooses exactly one result-returning path for a successful value.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <returns>The result of exactly one selected path, or the existing errors when the input failed.</returns>
    public RailResult<TNext> Branch<TNext>(Func<T, bool> predicate,
        Func<T, RailResult<TNext>> whenTrue, Func<T, RailResult<TNext>> whenFalse)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        return Then(value => predicate(value) ? whenTrue(value) : whenFalse(value));
    }

    /// <summary>Chooses exactly one asynchronous path using a synchronous predicate.</summary>
    /// <remarks>Forwards cancellation to the selected path; captures exceptions except cancellation.</remarks>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the result of exactly one selected path, or the existing errors when the input failed.</returns>
    public Task<RailResult<TNext>> BranchAsync<TNext>(Func<T, bool> predicate,
        Func<T, CancellationToken, Task<RailResult<TNext>>> whenTrue,
        Func<T, CancellationToken, Task<RailResult<TNext>>> whenFalse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        return ThenAsync((value, ct) => predicate(value) ? whenTrue(value, ct) : whenFalse(value, ct),
            cancellationToken);
    }

    /// <summary>Awaits a predicate and executes exactly one asynchronous path on success.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="predicate">Selects whenTrue or whenFalse for a successful value; failed inputs skip branching.</param>
    /// <param name="whenTrue">The result-producing path used when the predicate returns true.</param>
    /// <param name="whenFalse">The result-producing path used when the predicate returns false.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the result of exactly one selected path, or the existing errors when the input failed.</returns>
    public Task<RailResult<TNext>> BranchAsync<TNext>(Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task<RailResult<TNext>>> whenTrue,
        Func<T, CancellationToken, Task<RailResult<TNext>>> whenFalse,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        return ThenAsync(async (value, ct) => await predicate(value, ct).ConfigureAwait(false)
            ? await whenTrue(value, ct).ConfigureAwait(false)
            : await whenFalse(value, ct).ConfigureAwait(false), cancellationToken);
    }

    /// <summary>Transforms a successful value asynchronously; failures skip the selector. Captures callback exceptions except cancellation.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="selector">Transforms a successful value into the next payload.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning a result with the transformed payload, or the existing errors when the input failed.</returns>
    public async Task<RailResult<TNext>> SelectAsync<TNext>(
        Func<T, CancellationToken, Task<TNext>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try { return RailResult<TNext>.Passed(await selector(Value, cancellationToken).ConfigureAwait(false)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

    /// <summary>Chains an asynchronous result-returning operation on success; failures skip it. Captures callback exceptions except cancellation.</summary>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result on success, or the existing errors when the input failed.</returns>
    public async Task<RailResult<TNext>> ThenAsync<TNext>(
        Func<T, CancellationToken, Task<RailResult<TNext>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try
        {
            return await operation(Value, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The operation returned a null result.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

    /// <summary>Observes success. Observer exceptions propagate to the caller.</summary>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <returns>The unchanged outcome after observing success. Observer exceptions propagate.</returns>
    public RailResult<T> OnSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsPassed) action(Value);
        return this;
    }

    /// <summary>Observes failure. Observer exceptions propagate to the caller.</summary>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <returns>The unchanged outcome after observing failure. Observer exceptions propagate.</returns>
    public RailResult<T> OnFailure(Action<IReadOnlyList<RailError>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailed) action(Errors);
        return this;
    }

    /// <summary>Awaits a success observer and returns this result. Observer exceptions propagate.</summary>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the unchanged outcome after observing success. Observer exceptions propagate.</returns>
    public async Task<RailResult<T>> OnSuccessAsync(Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsPassed) await action(Value, cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <summary>Awaits a failure observer and returns this result. Observer exceptions propagate.</summary>
    /// <param name="action">The callback for the matching outcome; skipped for the other outcome.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the unchanged outcome after observing failure. Observer exceptions propagate.</returns>
    public async Task<RailResult<T>> OnFailureAsync(Func<IReadOnlyList<RailError>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) await action(Errors, cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <summary>Awaits exactly one outcome handler. Handler exceptions propagate.</summary>
    /// <typeparam name="TOutput">The value returned by either outcome callback.</typeparam>
    /// <param name="passed">The callback used only when the result is successful.</param>
    /// <param name="failed">The callback used only when the result has failed.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the value produced by the callback matching the outcome. Callback exceptions propagate.</returns>
    public async Task<TOutput> MatchAsync<TOutput>(Func<T, CancellationToken, Task<TOutput>> passed,
        Func<IReadOnlyList<RailError>, CancellationToken, Task<TOutput>> failed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passed);
        ArgumentNullException.ThrowIfNull(failed);
        cancellationToken.ThrowIfCancellationRequested();
        return IsPassed
            ? await passed(Value, cancellationToken).ConfigureAwait(false)
            : await failed(Errors, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Handles exactly one outcome. Handler exceptions propagate to the caller.</summary>
    /// <typeparam name="TOutput">The value returned by either outcome callback.</typeparam>
    /// <param name="passed">The callback used only when the result is successful.</param>
    /// <param name="failed">The callback used only when the result has failed.</param>
    /// <returns>The value produced by the callback matching the outcome. Callback exceptions propagate.</returns>
    public TOutput Match<TOutput>(Func<T, TOutput> passed, Func<IReadOnlyList<RailError>, TOutput> failed)
    {
        ArgumentNullException.ThrowIfNull(passed);
        ArgumentNullException.ThrowIfNull(failed);
        return IsPassed ? passed(Value) : failed(Errors);
    }
}
