namespace LayerZero.Core;

/// <summary>
/// Handles a synchronous vertical-slice request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <returns>The operation result.</returns>
    Result<TResponse> Handle(TRequest request);
}
