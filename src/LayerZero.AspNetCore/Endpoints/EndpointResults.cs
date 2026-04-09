using LayerZero.Core;
using Microsoft.AspNetCore.Http;

namespace LayerZero.AspNetCore;

internal static class EndpointResults
{
    public static IResult From<TResponse>(Result<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : EndpointProblemDetails.FromFailure(result.Errors);
    }
}
