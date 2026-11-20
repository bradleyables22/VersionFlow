namespace VersionFlow.Extensions.Railway;

public static partial class RailResult
{
    /// <summary>Attempts every item sequentially and retains each input and outcome, including successes when other items fail.</summary>
    /// <remarks>Operation exceptions become item failures. Enumeration exceptions are recorded separately and end enumeration. Cancellation propagates.</remarks>
    public static RailBatchResult<TInput, TOutput> TraverseResults<TInput, TOutput>(
        IEnumerable<TInput> items, Func<TInput, RailResult<TOutput>> operation)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        var entries = new List<RailBatchEntry<TInput, TOutput>>();
        var enumerationErrors = new List<RailError>();
        try
        {
            foreach (var item in items)
                entries.Add(new(item, RailResult<TInput>.Passed(item).Then(operation)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { enumerationErrors.AddRange(RailResult<TOutput>.Failed(exception).Errors); }
        return new(entries, enumerationErrors);
    }

    /// <summary>Awaits every item sequentially and retains each input and outcome, including successes when other items fail.</summary>
    /// <remarks>Operation exceptions become item failures. Enumeration exceptions are recorded separately and end enumeration. Cancellation propagates.</remarks>
    public static async Task<RailBatchResult<TInput, TOutput>> TraverseResultsAsync<TInput, TOutput>(
        IEnumerable<TInput> items, Func<TInput, CancellationToken, Task<RailResult<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        var entries = new List<RailBatchEntry<TInput, TOutput>>();
        var enumerationErrors = new List<RailError>();
        try
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RailResult<TInput>.Passed(item).ThenAsync(operation, cancellationToken).ConfigureAwait(false);
                entries.Add(new(item, result));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { enumerationErrors.AddRange(RailResult<TOutput>.Failed(exception).Errors); }
        return new(entries, enumerationErrors);
    }
}

