namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    /// <summary>Runs an operation on success only when the predicate matches; otherwise returns this result.</summary>
    public RailResult<T> When(Func<T, bool> predicate, Func<T, RailResult<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(operation);
        return IsFailed ? this : Branch(predicate, operation, _ => this);
    }

    /// <summary>Conditionally runs an asynchronous operation using a synchronous predicate.</summary>
    /// <remarks>Preserves skipped results. Captures callback exceptions except cancellation.</remarks>
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
