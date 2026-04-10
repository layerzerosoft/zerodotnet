namespace LayerZero.Http;

/// <summary>
/// Creates strongly typed HTTP endpoint contracts.
/// </summary>
public static class HttpEndpoint
{
    /// <summary>
    /// Creates a GET endpoint that returns a success payload.
    /// </summary>
    public static GetEndpoint<TRequest, TResponse> Get<TRequest, TResponse>(string template) => new(template);

    /// <summary>
    /// Creates a GET endpoint that returns no success payload.
    /// </summary>
    public static GetEndpoint<TRequest> Get<TRequest>(string template) => new(template);

    /// <summary>
    /// Creates a POST endpoint that returns a success payload.
    /// </summary>
    public static PostEndpoint<TRequest, TResponse> Post<TRequest, TResponse>(string template) => new(template);

    /// <summary>
    /// Creates a POST endpoint that returns no success payload.
    /// </summary>
    public static PostEndpoint<TRequest> Post<TRequest>(string template) => new(template);

    /// <summary>
    /// Creates a PUT endpoint that returns a success payload.
    /// </summary>
    public static PutEndpoint<TRequest, TResponse> Put<TRequest, TResponse>(string template) => new(template);

    /// <summary>
    /// Creates a PUT endpoint that returns no success payload.
    /// </summary>
    public static PutEndpoint<TRequest> Put<TRequest>(string template) => new(template);

    /// <summary>
    /// Creates a PATCH endpoint that returns a success payload.
    /// </summary>
    public static PatchEndpoint<TRequest, TResponse> Patch<TRequest, TResponse>(string template) => new(template);

    /// <summary>
    /// Creates a PATCH endpoint that returns no success payload.
    /// </summary>
    public static PatchEndpoint<TRequest> Patch<TRequest>(string template) => new(template);

    /// <summary>
    /// Creates a DELETE endpoint that returns a success payload.
    /// </summary>
    public static DeleteEndpoint<TRequest, TResponse> Delete<TRequest, TResponse>(string template) => new(template);

    /// <summary>
    /// Creates a DELETE endpoint that returns no success payload.
    /// </summary>
    public static DeleteEndpoint<TRequest> Delete<TRequest>(string template) => new(template);
}
