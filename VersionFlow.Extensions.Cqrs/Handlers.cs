namespace VersionFlow.Extensions.Cqrs;

/// <summary>
/// Handles one application operation asynchronously and returns a result.
/// </summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
/// <typeparam name="TResponse">The operation result.</typeparam>
public interface ICqrsAsyncHandler<in TRequest, TResponse>
{
    /// <summary>Executes this handler directly; register the implementation in DI and inject its handler interface.</summary>
    /// <param name="request">The application operation input.</param>
    /// <param name="cancellationToken">Cancellation to observe during the operation.</param>
    /// <returns>The asynchronous operation result.</returns>
    Task<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles one application operation asynchronously without a response payload.
/// </summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
public interface ICqrsAsyncHandler<in TRequest>
{
    /// <summary>Executes this handler directly for an operation with no response payload.</summary>
    /// <param name="request">The application operation input.</param>
    /// <param name="cancellationToken">Cancellation to observe during the operation.</param>
    /// <returns>A task completing when the operation finishes.</returns>
    Task HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}


/// <summary>Handles one application operation synchronously and returns a result.</summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
/// <typeparam name="TResponse">The operation result.</typeparam>
public interface ICqrsHandler<in TRequest, TResponse>
{
    /// <summary>Executes the operation synchronously and returns its response directly.</summary>
    /// <param name="request">The application operation input.</param>
    /// <param name="cancellationToken">Optional cancellation for the implementation to observe during the operation.</param>
    /// <returns>The operation result.</returns>
    /// <remarks>Exceptions propagate directly to the caller. Use ICqrsAsyncHandler for asynchronous I/O.</remarks>
    TResponse Handle(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Handles one application operation synchronously without a response payload.</summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
public interface ICqrsHandler<in TRequest>
{
    /// <summary>Executes synchronous work directly and returns when it completes.</summary>
    /// <param name="request">The application operation input.</param>
    /// <param name="cancellationToken">Optional cancellation for the implementation to observe during the operation.</param>
    /// <remarks>Exceptions propagate directly to the caller. Use ICqrsAsyncHandler for asynchronous I/O.</remarks>
    void Handle(TRequest request, CancellationToken cancellationToken = default);
}
