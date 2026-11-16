namespace VersionFlow.Extensions.Results;

/// <summary>Checks successful payloads, preserving the first failure. Callback exceptions propagate.</summary>
public static class TryResultExtensions
{
    /// <summary>Fails when the predicate matches. Successful null payloads are passed to the predicate.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <returns>The original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static TryResult<T> FailWhen<T>(this TryResult<T> result, Func<T, bool> predicate, string message)
    {
        Validate(result, predicate, message);
        return result.Success && predicate(result.Data!) ? TryResult<T>.Failed(message) : result;
    }

    /// <summary>Checks a successful payload asynchronously.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <returns>A task returning the original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static Task<TryResult<T>> FailWhenAsync<T>(this TryResult<T> result, Func<T, Task<bool>> predicate, string message)
    {
        Validate(result, predicate, message);
        return result.FailWhenAsync((value, _) => predicate(value), message);
    }

    /// <summary>Checks a successful payload asynchronously, forwarding cancellation to the predicate.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static async Task<TryResult<T>> FailWhenAsync<T>(this TryResult<T> result,
        Func<T, CancellationToken, Task<bool>> predicate, string message, CancellationToken cancellationToken = default)
    {
        Validate(result, predicate, message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Success) return result;
        var fail = await predicate(result.Data!, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return fail ? TryResult<T>.Failed(message) : result;
    }

    /// <summary>Appends a synchronous check to an asynchronous result chain.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <returns>The original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static async Task<TryResult<T>> FailWhen<T>(this Task<TryResult<T>> result, Func<T, bool> predicate, string message)
    {
        Validate(result, predicate, message);
        return (await result.ConfigureAwait(false)).FailWhen(predicate, message);
    }

    /// <summary>Appends an asynchronous check to an asynchronous result chain.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <returns>A task returning the original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static async Task<TryResult<T>> FailWhenAsync<T>(this Task<TryResult<T>> result, Func<T, Task<bool>> predicate, string message)
    {
        Validate(result, predicate, message);
        return await (await result.ConfigureAwait(false)).FailWhenAsync(predicate, message).ConfigureAwait(false);
    }

    /// <summary>Appends a cancellable asynchronous check. Cancellation also stops waiting for the upstream result.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="result">The upstream outcome. Existing failures skip validation callbacks.</param>
    /// <param name="predicate">True fails a successful result; false preserves it. Successful null payloads are passed through.</param>
    /// <param name="message">A nonblank failure description. Use text safe to expose to the caller.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the original result if already failed or the predicate is false; otherwise a failure with the supplied message.</returns>
    public static async Task<TryResult<T>> FailWhenAsync<T>(this Task<TryResult<T>> result,
        Func<T, CancellationToken, Task<bool>> predicate, string message, CancellationToken cancellationToken = default)
    {
        Validate(result, predicate, message);
        var outcome = await result.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.FailWhenAsync(predicate, message, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(object result, Delegate predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicate);
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A failure requires a message.", nameof(message));
    }
}
