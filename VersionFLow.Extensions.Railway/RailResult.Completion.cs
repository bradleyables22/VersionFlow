namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    /// <summary>Observes either outcome and returns this same result. Observer exceptions propagate.</summary>
    public RailResult<T> OnCompleted(Action<RailResult<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(this);
        return this;
    }

    /// <summary>Awaits an observer of either outcome and returns this same result. Exceptions and cancellation propagate.</summary>
    public async Task<RailResult<T>> OnCompletedAsync(Func<RailResult<T>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        await action(this, cancellationToken).ConfigureAwait(false);
        return this;
    }
}

public static partial class RailResultTaskExtensions
{
    /// <summary>Awaits the source, observes either result outcome, and returns the same result. Upstream faults propagate.</summary>
    public static async Task<RailResult<T>> OnCompleted<T>(this Task<RailResult<T>> source,
        Action<RailResult<T>> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return result.OnCompleted(action);
    }

    /// <summary>Awaits the source and completion observer. Cancellation of the wait does not cancel upstream work.</summary>
    public static async Task<RailResult<T>> OnCompletedAsync<T>(this Task<RailResult<T>> source,
        Func<RailResult<T>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null result.");
        return await result.OnCompletedAsync(action, cancellationToken).ConfigureAwait(false);
    }
}
