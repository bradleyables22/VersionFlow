namespace VersionFlow.Extensions.Railway;

/// <summary>Controls whether collection processing stops at a failed item or collects all item errors.</summary>
public enum RailFailureMode
{
    /// <summary>Stops enumeration when the first failed item is encountered.</summary>
    StopOnFirstFailure,
    /// <summary>Processes every item and combines failure details in input order.</summary>
    CollectAll
}

/// <summary>Operations for ordered collections of railway results.</summary>
public static class RailResult
{
    /// <summary>Combines results into a read-only list, collecting all errors by default.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="results">The results to combine in input order.</param>
    /// <param name="failureMode">Whether to stop on the first failure or collect every failure; successful values are returned only when all processed items pass.</param>
    /// <returns>A successful read-only list when all processed results pass, or a failure containing the collected errors.</returns>
    public static RailResult<IReadOnlyList<T>> Combine<T>(IEnumerable<RailResult<T>> results,
        RailFailureMode failureMode = RailFailureMode.CollectAll)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Traverse(results, result => result, failureMode);
    }

    /// <summary>Processes items sequentially, stopping at the first failure by default.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="items">The source items to process sequentially in input order.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="failureMode">Whether to stop on the first failure or collect every failure; successful values are returned only when all processed items pass.</param>
    /// <returns>A successful read-only list when all processed items pass, or a failure containing the collected errors.</returns>
    public static RailResult<IReadOnlyList<TNext>> Traverse<T, TNext>(IEnumerable<T> items,
        Func<T, RailResult<TNext>> operation, RailFailureMode failureMode = RailFailureMode.StopOnFirstFailure)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ValidateMode(failureMode);
        var values = new List<TNext>();
        var errors = new List<RailError>();
        try
        {
            foreach (var item in items)
            {
                var result = RailResult<T>.Passed(item).Then(operation);
                if (result.IsPassed) values.Add(result.Value);
                else
                {
                    errors.AddRange(result.Errors);
                    if (failureMode == RailFailureMode.StopOnFirstFailure) break;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { errors.AddRange(RailResult<TNext>.Failed(exception).Errors); }
        return Finish(values, errors);
    }

    /// <summary>Awaits items sequentially, forwarding cancellation and preserving input order.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <typeparam name="TNext">The next payload or item type.</typeparam>
    /// <param name="items">The source items to process sequentially in input order.</param>
    /// <param name="operation">The operation to invoke; must not return a null result object.</param>
    /// <param name="failureMode">Whether to stop on the first failure or collect every failure; successful values are returned only when all processed items pass.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning a successful read-only list when all processed items pass, or a failure containing the collected errors.</returns>
    public static async Task<RailResult<IReadOnlyList<TNext>>> TraverseAsync<T, TNext>(IEnumerable<T> items,
        Func<T, CancellationToken, Task<RailResult<TNext>>> operation,
        RailFailureMode failureMode = RailFailureMode.StopOnFirstFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(operation);
        ValidateMode(failureMode);
        cancellationToken.ThrowIfCancellationRequested();
        var values = new List<TNext>();
        var errors = new List<RailError>();
        try
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RailResult<T>.Passed(item).ThenAsync(operation, cancellationToken).ConfigureAwait(false);
                if (result.IsPassed) values.Add(result.Value);
                else
                {
                    errors.AddRange(result.Errors);
                    if (failureMode == RailFailureMode.StopOnFirstFailure) break;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { errors.AddRange(RailResult<TNext>.Failed(exception).Errors); }
        return Finish(values, errors);
    }

    /// <summary>Combines asynchronous results in input order. Already-started tasks are not cancelled.</summary>
    /// <typeparam name="T">The payload or item type.</typeparam>
    /// <param name="results">The results to combine in input order.</param>
    /// <param name="failureMode">Whether to stop on the first failure or collect every failure; successful values are returned only when all processed items pass.</param>
    /// <param name="cancellationToken">Cancellation to observe while waiting and pass to token-aware callbacks. Cancellation propagates instead of becoming a failure result.</param>
    /// <returns>A task returning a successful read-only list when all processed results pass, or a failure containing the collected errors.</returns>
    public static Task<RailResult<IReadOnlyList<T>>> CombineAsync<T>(IEnumerable<Task<RailResult<T>>> results,
        RailFailureMode failureMode = RailFailureMode.CollectAll,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        return TraverseAsync(results, (result, ct) => result.WaitAsync(ct), failureMode, cancellationToken);
    }

    private static void ValidateMode(RailFailureMode failureMode)
    {
        if (!Enum.IsDefined(failureMode))
            throw new ArgumentOutOfRangeException(nameof(failureMode));
    }

    private static RailResult<IReadOnlyList<T>> Finish<T>(List<T> values, List<RailError> errors) =>
        errors.Count > 0
            ? RailResult<IReadOnlyList<T>>.Failed(errors)
            : RailResult<IReadOnlyList<T>>.Passed(values.AsReadOnly());
}
