namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    /// <summary>Runs an operation, wrapping its value or capturing an exception except cancellation.</summary>
    public static RailResult<T> Try(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try { return Passed(operation()); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(exception); }
    }

    /// <summary>Awaits an operation, wrapping its value or capturing an exception except cancellation.</summary>
    public static async Task<RailResult<T>> TryAsync(Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        try { return Passed(await operation(cancellationToken).ConfigureAwait(false)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(exception); }
    }

    /// <summary>Replaces a failure with a fallback result. A successful result is unchanged.</summary>
    public RailResult<T> Recover(Func<IReadOnlyList<RailError>, RailResult<T>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        if (IsPassed) return this;
        try { return recovery(Errors) ?? throw new InvalidOperationException("Recovery returned a null result."); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(Errors.Concat(Failed(exception).Errors)); }
    }

    /// <summary>Awaits a fallback on failure. Thrown exceptions are appended to the original errors.</summary>
    public async Task<RailResult<T>> RecoverAsync(
        Func<IReadOnlyList<RailError>, CancellationToken, Task<RailResult<T>>> recovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsPassed) return this;
        try
        {
            return await recovery(Errors, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Recovery returned a null result.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(Errors.Concat(Failed(exception).Errors)); }
    }

    /// <summary>Maps each error in order. Successful results are unchanged.</summary>
    /// <remarks>If mapping fails, original errors are preserved and the exception is appended.</remarks>
    public RailResult<T> MapErrors(Func<RailError, RailError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (IsPassed) return this;
        try { return Failed(Errors.Select(mapper)); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(Errors.Concat(Failed(exception).Errors)); }
    }

    /// <summary>Maps errors sequentially. Mapping failures preserve original errors and append the exception.</summary>
    public async Task<RailResult<T>> MapErrorsAsync(Func<RailError, CancellationToken, Task<RailError>> mapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsPassed) return this;
        try
        {
            var mapped = new List<RailError>(Errors.Count);
            foreach (var error in Errors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                mapped.Add(await mapper(error, cancellationToken).ConfigureAwait(false));
            }
            return Failed(mapped);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Failed(Errors.Concat(Failed(exception).Errors)); }
    }
}
