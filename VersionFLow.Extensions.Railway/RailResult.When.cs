namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    /// <summary>Runs an operation on success only when the predicate matches; otherwise returns this result.</summary>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <returns>The callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public RailResult<T> When(Func<T, bool> predicate, Func<T, RailResult<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        return IsFailed ? this : Branch(predicate, operation, _ => this);
    }

    /// <summary>Conditionally runs an asynchronous operation using a synchronous predicate.</summary>
    /// <remarks>Preserves skipped results. Captures callback exceptions except cancellation.</remarks>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public Task<RailResult<T>> WhenAsync(Func<T, bool> predicate,
        Func<T, CancellationToken, Task<RailResult<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return IsFailed ? Task.FromResult(this)
            : BranchAsync(predicate, operation, (_, _) => Task.FromResult(this), cancellationToken);
    }

    /// <summary>Awaits a predicate and runs an asynchronous operation only when it matches.</summary>
    /// <remarks>Preserves skipped results and forwards cancellation to both callbacks.</remarks>
    /// <param name="predicate">True runs the operation on a successful value; false leaves the result unchanged.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning the callback result when successful and the predicate matches; otherwise the unchanged input.</returns>
    public Task<RailResult<T>> WhenAsync(Func<T, CancellationToken, Task<bool>> predicate,
        Func<T, CancellationToken, Task<RailResult<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return IsFailed ? Task.FromResult(this)
            : BranchAsync(predicate, operation, (_, _) => Task.FromResult(this), cancellationToken);
    }
}
