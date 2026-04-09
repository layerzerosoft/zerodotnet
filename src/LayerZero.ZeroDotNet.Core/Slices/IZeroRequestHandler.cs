namespace LayerZero.ZeroDotNet;

/// <summary>
/// Handles a synchronous vertical-slice request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IZeroRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <returns>The operation result.</returns>
    ZeroResult<TResponse> Handle(TRequest request);
}
