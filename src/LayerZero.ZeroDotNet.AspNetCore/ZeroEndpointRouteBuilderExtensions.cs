using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.ZeroDotNet.AspNetCore;

/// <summary>
/// Maps ZeroDotNet vertical slices to Minimal API endpoints.
/// </summary>
public static class ZeroEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a synchronous GET vertical slice.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapZeroGet<TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IZeroRequestHandler<ZeroUnit, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapGet(pattern, ([FromServices] THandler handler) => ZeroEndpointResults.From(handler.Handle(ZeroUnit.Value)))
            .WithZeroOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps an asynchronous GET vertical slice.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapZeroGetAsync<TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IZeroAsyncRequestHandler<ZeroUnit, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapGet(pattern, async ([FromServices] THandler handler, HttpContext httpContext) =>
                ZeroEndpointResults.From(await handler.HandleAsync(ZeroUnit.Value, httpContext.RequestAborted).ConfigureAwait(false)))
            .WithZeroOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps a synchronous POST vertical slice and applies ZeroDotNet request validation.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapZeroPost<TRequest, TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IZeroRequestHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapPost(pattern, ([FromBody] TRequest request, [FromServices] THandler handler) =>
                ZeroEndpointResults.From(handler.Handle(request)))
            .AddEndpointFilter<ZeroValidationEndpointFilter<TRequest>>()
            .WithZeroOpenApiDefaults<TResponse>();
    }

    /// <summary>
    /// Maps an asynchronous POST vertical slice and applies ZeroDotNet request validation.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapZeroPostAsync<TRequest, TResponse, THandler>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THandler : class, IZeroAsyncRequestHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints
            .MapPost(pattern, async (
                [FromBody] TRequest request,
                [FromServices] THandler handler,
                HttpContext httpContext) =>
                ZeroEndpointResults.From(await handler.HandleAsync(request, httpContext.RequestAborted).ConfigureAwait(false)))
            .AddEndpointFilter<ZeroValidationEndpointFilter<TRequest>>()
            .WithZeroOpenApiDefaults<TResponse>();
    }

    private static RouteHandlerBuilder WithZeroOpenApiDefaults<TResponse>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
