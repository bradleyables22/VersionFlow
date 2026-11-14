namespace VersionFlow.Extensions.Railway;

/// <summary>An immutable success value or a nonempty, read-only collection of errors.</summary>
/// <remarks>Values are not cloned. Prefer immutable values when applying recovery.</remarks>
public sealed class RailResult<T>
{
    private readonly T? _value;

    private RailResult(T value)
    {
        _value = value;
        IsPassed = true;
        Errors = Array.Empty<RailError>();
    }

    private RailResult(IReadOnlyList<RailError> errors) => Errors = errors;

    public bool IsPassed { get; }
    public bool IsFailed => !IsPassed;
    public IReadOnlyList<RailError> Errors { get; }
    public T Value => IsPassed ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static RailResult<T> Passed(T value) => new(value);

    public static RailResult<T> Failed(string message, RailErrorCode code = default) =>
        Failed(new RailError(message, code));

    public static RailResult<T> Failed(Exception exception, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failed(new RailError(
            string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message,
            code ?? RailErrorCodes.Exception, exception));
    }

    public static RailResult<T> Failed(string message, Exception exception, RailErrorCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Failed(new RailError(message, code ?? RailErrorCodes.Exception, exception));
    }

    public static RailResult<T> Failed(RailError error) => Failed(new[] { error });

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
    public RailResult<T> Ensure(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureTrue(predicate, message, recover, code);

    /// <summary>Compatibility alias for EnsureTrue; the predicate must return true to pass.</summary>
    public RailResult<T> Ensure(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureTrue(predicate, error, recover);

    /// <summary>Requires true, optionally correcting the value once and rechecking for true.</summary>
    public RailResult<T> EnsureTrue(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureTrue(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover);

    /// <summary>Requires true, optionally correcting the value once and rechecking for true.</summary>
    public RailResult<T> EnsureTrue(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureCore(predicate, true, error, recover);

    /// <summary>Requires false, optionally correcting the value once and rechecking for false.</summary>
    public RailResult<T> EnsureFalse(Func<T, bool> predicate, string message = "Validation failed.",
        Func<T, T>? recover = null, RailErrorCode? code = null) =>
        EnsureFalse(predicate, new RailError(message, code ?? RailErrorCodes.ValidationFailed), recover);

    /// <summary>Requires false, optionally correcting the value once and rechecking for false.</summary>
    public RailResult<T> EnsureFalse(Func<T, bool> predicate, RailError error, Func<T, T>? recover = null)
        => EnsureCore(predicate, false, error, recover);

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
    public RailResult<TNext> Select<TNext>(Func<T, TNext> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try { return RailResult<TNext>.Passed(selector(Value)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

    /// <summary>Chains an operation that returns its own result.</summary>
    public RailResult<TNext> Then<TNext>(Func<T, RailResult<TNext>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (IsFailed) return RailResult<TNext>.Failed(Errors);
        try { return operation(Value) ?? throw new InvalidOperationException("The operation returned a null result."); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return RailResult<TNext>.Failed(exception); }
    }

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
    public RailResult<T> OnSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsPassed) action(Value);
        return this;
    }

    /// <summary>Observes failure. Observer exceptions propagate to the caller.</summary>
    public RailResult<T> OnFailure(Action<IReadOnlyList<RailError>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailed) action(Errors);
        return this;
    }

    /// <summary>Handles exactly one outcome. Handler exceptions propagate to the caller.</summary>
    public TOutput Match<TOutput>(Func<T, TOutput> passed, Func<IReadOnlyList<RailError>, TOutput> failed)
    {
        ArgumentNullException.ThrowIfNull(passed);
        ArgumentNullException.ThrowIfNull(failed);
        return IsPassed ? passed(Value) : failed(Errors);
    }
}
