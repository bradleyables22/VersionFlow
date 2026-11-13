namespace VersionFlow.Extensions.Cqrs;

/// <summary>
/// Handles one application operation and returns a result.
/// </summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
/// <typeparam name="TResponse">The operation result.</typeparam>
public interface ICqrsHandler<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles one application operation that does not return a result.
/// </summary>
/// <typeparam name="TRequest">The operation input.</typeparam>
public interface ICqrsHandler<in TRequest>
{
    Task HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

