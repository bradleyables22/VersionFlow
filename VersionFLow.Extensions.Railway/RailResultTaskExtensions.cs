namespace VersionFlow.Extensions.Railway;

/// <summary>Composes synchronous and asynchronous steps after a Task of a railway result.</summary>
/// <remarks>Upstream task faults propagate. Cancellation stops waiting but does not cancel upstream work.</remarks>
public static class RailResultTaskExtensions
{
    /// <summary>Awaits the source and delegates to Ensure, preserving its outcome semantics.</summary>
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
