namespace LayerZero.Core;

/// <summary>
/// Handles an asynchronous vertical-slice request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IAsyncRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request asynchronously.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    ValueTask<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
