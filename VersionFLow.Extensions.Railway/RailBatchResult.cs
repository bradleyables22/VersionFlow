namespace VersionFlow.Extensions.Railway;

/// <summary>An original input paired with its operation's outcome.</summary>
public sealed record RailBatchEntry<TInput, TOutput>(TInput Input, RailResult<TOutput> Result);

/// <summary>An immutable snapshot of all attempted items, including successful and failed outcomes.</summary>
public sealed class RailBatchResult<TInput, TOutput>
{
    internal RailBatchResult(List<RailBatchEntry<TInput, TOutput>> entries, List<RailError> enumerationErrors)
    {
        Entries = Array.AsReadOnly(entries.ToArray());
        Results = Array.AsReadOnly(entries.Select(e => e.Result).ToArray());
        SuccessfulValues = Array.AsReadOnly(entries.Where(e => e.Result.IsPassed).Select(e => e.Result.Value).ToArray());
        Failures = Array.AsReadOnly(entries.Where(e => e.Result.IsFailed).ToArray());
        EnumerationErrors = Array.AsReadOnly(enumerationErrors.ToArray());
        Errors = Array.AsReadOnly(entries.SelectMany(e => e.Result.Errors).Concat(enumerationErrors).ToArray());
    }

    /// <summary>All attempted inputs and their outcomes in input order, including duplicate inputs.</summary>
    public IReadOnlyList<RailBatchEntry<TInput, TOutput>> Entries { get; }
    /// <summary>Every individual result in input order.</summary>
    public IReadOnlyList<RailResult<TOutput>> Results { get; }
    /// <summary>Successful output values in input order.</summary>
    public IReadOnlyList<TOutput> SuccessfulValues { get; }
    /// <summary>Failed items with their original inputs. Count represents failed items, not error count.</summary>
    public IReadOnlyList<RailBatchEntry<TInput, TOutput>> Failures { get; }
    /// <summary>Errors from enumerating the source, which cannot be associated with an input item.</summary>
    public IReadOnlyList<RailError> EnumerationErrors { get; }
    /// <summary>Item errors in input order, followed by any source enumeration errors.</summary>
    public IReadOnlyList<RailError> Errors { get; }
    /// <summary>Whether all items passed and enumeration completed without errors. Empty batches pass.</summary>
    public bool IsPassed => Errors.Count == 0;
    /// <summary>Whether any item or source enumeration failed.</summary>
    public bool IsFailed => !IsPassed;

    /// <summary>Observes a failed batch and returns this same batch. Observer exceptions propagate.</summary>
    public RailBatchResult<TInput, TOutput> OnFailure(Action<RailBatchResult<TInput, TOutput>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailed) action(this);
        return this;
    }

    /// <summary>Awaits the failure observer and returns this same batch. Exceptions and cancellation propagate.</summary>
    public async Task<RailBatchResult<TInput, TOutput>> OnFailureAsync(
        Func<RailBatchResult<TInput, TOutput>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) await action(this, cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <summary>Observes the completed batch and returns this same batch. Observer exceptions propagate.</summary>
    public RailBatchResult<TInput, TOutput> OnCompleted(Action<RailBatchResult<TInput, TOutput>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(this);
        return this;
    }

    /// <summary>Awaits the completion observer and returns this same batch. Exceptions and cancellation propagate.</summary>
    public async Task<RailBatchResult<TInput, TOutput>> OnCompletedAsync(
        Func<RailBatchResult<TInput, TOutput>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        await action(this, cancellationToken).ConfigureAwait(false);
        return this;
    }
}

/// <summary>Fluent observers for pending batch results. Upstream faults propagate.</summary>
public static class RailBatchResultTaskExtensions
{

    /// <summary>Awaits the batch and observer, preserving the same batch. Cancellation of a wait does not cancel upstream work.</summary>
    public static async Task<RailBatchResult<TInput, TOutput>> OnFailure<TInput, TOutput>(
        this Task<RailBatchResult<TInput, TOutput>> source,
        Action<RailBatchResult<TInput, TOutput>> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null batch.");
        return result.OnFailure(action);
    }

    /// <summary>Awaits the batch and asynchronous observer, preserving the same batch. Cancellation of a wait does not cancel upstream work.</summary>
    public static async Task<RailBatchResult<TInput, TOutput>> OnFailureAsync<TInput, TOutput>(
        this Task<RailBatchResult<TInput, TOutput>> source,
        Func<RailBatchResult<TInput, TOutput>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null batch.");
        return await result.OnFailureAsync(action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the batch and observer, preserving the same batch. Cancellation of a wait does not cancel upstream work.</summary>
    public static async Task<RailBatchResult<TInput, TOutput>> OnCompleted<TInput, TOutput>(
        this Task<RailBatchResult<TInput, TOutput>> source,
        Action<RailBatchResult<TInput, TOutput>> action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null batch.");
        return result.OnCompleted(action);
    }

    /// <summary>Awaits the batch and asynchronous observer, preserving the same batch. Cancellation of a wait does not cancel upstream work.</summary>
    public static async Task<RailBatchResult<TInput, TOutput>> OnCompletedAsync<TInput, TOutput>(
        this Task<RailBatchResult<TInput, TOutput>> source,
        Func<RailBatchResult<TInput, TOutput>, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        var result = await source.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The source task returned a null batch.");
        return await result.OnCompletedAsync(action, cancellationToken).ConfigureAwait(false);
    }
}

