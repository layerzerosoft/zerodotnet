using LayerZero.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.AspNetCore;

/// <summary>
/// Maps LayerZero vertical slices to Minimal API endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a synchronous GET vertical slice.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapGetSlice<TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IRequestHandler<Unit, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapGet(pattern, ([FromServices] THandler handler) => EndpointResults.From(handler.Handle(Unit.Value)))
            .WithOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps an asynchronous GET vertical slice.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapGetSliceAsync<TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IAsyncRequestHandler<Unit, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapGet(pattern, async ([FromServices] THandler handler, HttpContext httpContext) =>
                EndpointResults.From(await handler.HandleAsync(Unit.Value, httpContext.RequestAborted).ConfigureAwait(false)))
            .WithOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps a synchronous POST vertical slice and applies LayerZero request validation.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapPostSlice<TRequest, TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapPost(pattern, ([FromBody] TRequest request, [FromServices] THandler handler) =>
                EndpointResults.From(handler.Handle(request)))
            .Validate<TRequest>()
            .WithOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps an asynchronous POST vertical slice and applies LayerZero request validation.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapPostSliceAsync<TRequest, TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IAsyncRequestHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapPost(pattern, async (
                [FromBody] TRequest request,
                [FromServices] THandler handler,
                HttpContext httpContext) =>
                EndpointResults.From(await handler.HandleAsync(request, httpContext.RequestAborted).ConfigureAwait(false)))
            .Validate<TRequest>()
            .WithOpenApiDefaults<TResponse>();
    }

    private static RouteHandlerBuilder WithOpenApiDefaults<TResponse>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
