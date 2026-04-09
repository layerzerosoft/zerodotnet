using Microsoft.AspNetCore.Routing;

namespace LayerZero.AspNetCore;

/// <summary>
/// Defines a Minimal API slice that maps its own endpoints.
/// </summary>
/// <remarks>
/// Endpoint slices keep ASP.NET Core in the developer's hands: implementations
/// call the native Minimal API mapping APIs directly and LayerZero only
/// discovers and invokes the static mapping entry point.
/// </remarks>
public interface IEndpointSlice
{
    /// <summary>
    /// Maps the slice endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    static abstract void MapEndpoint(IEndpointRouteBuilder endpoints);
}
