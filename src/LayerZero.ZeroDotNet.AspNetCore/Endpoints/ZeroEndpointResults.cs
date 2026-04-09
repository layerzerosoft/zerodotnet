using Microsoft.AspNetCore.Http;

namespace LayerZero.ZeroDotNet.AspNetCore;

internal static class ZeroEndpointResults
{
    public static IResult From<TResponse>(ZeroResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ZeroEndpointProblemDetails.FromFailure(result.Errors);
    }
}
