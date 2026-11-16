namespace VersionFlow.Extensions.Railway;

public sealed partial class RailResult<T>
{
    private RailResult<T> EnsureCore(Func<T, bool> predicate, bool expected, RailError error,
        Func<T, T>? recover, Action<T, bool>? onPassed = null, Action<RailError>? onFailed = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        // Validate arguments even if an earlier failure skips this rule.
        var failure = Failed(error);
        if (IsFailed) return this;

        var (result, wasRecovered) = EvaluateValidation(predicate, expected, error, recover, failure);
        // Observers are outside evaluation's catch blocks: their exceptions propagate.
        if (result.IsPassed) onPassed?.Invoke(result.Value, wasRecovered);
        else onFailed?.Invoke(result.Errors[0]);
        return result;
    }

    private async Task<RailResult<T>> EnsureCoreAsync(
        Func<T, CancellationToken, Task<bool>> predicate, bool expected, RailError error,
        Func<T, CancellationToken, Task<T>>? recover, CancellationToken cancellationToken,
        Action<T, bool>? onPassed = null, Action<RailError>? onFailed = null,
        Func<T, bool, CancellationToken, Task>? onPassedAsync = null,
        Func<RailError, CancellationToken, Task>? onFailedAsync = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateEnsureObservers(onPassed, onFailed, onPassedAsync, onFailedAsync);
        var failure = Failed(error);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsFailed) return this;

        var (result, wasRecovered) = await EvaluateValidationAsync(
            predicate, expected, error, recover, failure, cancellationToken).ConfigureAwait(false);

        // Cancellation is not a failed validation and must not notify a failure observer.
        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsPassed)
        {
            onPassed?.Invoke(result.Value, wasRecovered);
            if (onPassedAsync is not null)
                await onPassedAsync(result.Value, wasRecovered, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            onFailed?.Invoke(result.Errors[0]);
            if (onFailedAsync is not null)
                await onFailedAsync(result.Errors[0], cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    internal static void ValidateEnsureObservers(Action<T, bool>? onPassed, Action<RailError>? onFailed,
        Func<T, bool, CancellationToken, Task>? onPassedAsync,
        Func<RailError, CancellationToken, Task>? onFailedAsync)
    {
        if (onPassed is not null && onPassedAsync is not null)
            throw new ArgumentException("Supply either onPassed or onPassedAsync, not both.", nameof(onPassedAsync));
        if (onFailed is not null && onFailedAsync is not null)
            throw new ArgumentException("Supply either onFailed or onFailedAsync, not both.", nameof(onFailedAsync));
    }

    private (RailResult<T> Result, bool WasRecovered) EvaluateValidation(
        Func<T, bool> predicate, bool expected, RailError error, Func<T, T>? recover, RailResult<T> failure)
    {
        try
        {
            if (predicate(Value) == expected) return (this, false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (Failed(new RailError(error.Message, error.Code, exception)), false);
        }

        if (recover is null) return (failure, false);
        try
        {
            var corrected = recover(Value);
            return predicate(corrected) == expected
                ? (Passed(corrected), true)
                : (Failed(error.WithRecovery(new RailRecoveryFailure("The corrected value did not satisfy the condition."))), false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (Failed(error.WithRecovery(new RailRecoveryFailure("Recovery or its validation threw an exception.", exception))), false);
        }
    }

    private async Task<(RailResult<T> Result, bool WasRecovered)> EvaluateValidationAsync(
        Func<T, CancellationToken, Task<bool>> predicate, bool expected, RailError error,
        Func<T, CancellationToken, Task<T>>? recover, RailResult<T> failure, CancellationToken cancellationToken)
    {
        try
        {
            if (await predicate(Value, cancellationToken).ConfigureAwait(false) == expected) return (this, false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (Failed(new RailError(error.Message, error.Code, exception)), false);
        }

        if (recover is null) return (failure, false);
        try
        {
            var corrected = await recover(Value, cancellationToken).ConfigureAwait(false);
            return await predicate(corrected, cancellationToken).ConfigureAwait(false) == expected
                ? (Passed(corrected), true)
                : (Failed(error.WithRecovery(new RailRecoveryFailure("The corrected value did not satisfy the condition."))), false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (Failed(error.WithRecovery(new RailRecoveryFailure("Recovery or its validation threw an exception.", exception))), false);
        }
    }
}
