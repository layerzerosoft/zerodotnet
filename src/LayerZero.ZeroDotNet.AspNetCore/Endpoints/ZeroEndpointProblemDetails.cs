using LayerZero.ZeroDotNet.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LayerZero.ZeroDotNet.AspNetCore;

internal static class ZeroEndpointProblemDetails
{
    public static IResult FromValidation(ZeroValidationResult validation)
    {
        Dictionary<string, string[]> errors = validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);

        HttpValidationProblemDetails details = new(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed.",
            Type = "https://zerodotnet.dev/problems/validation",
        };

        details.Extensions["zero.errors"] = validation.Errors
            .Select(error => new
            {
                error.Code,
                error.PropertyName,
                error.Message,
            })
            .ToArray();

        return Results.Json(
            details,
            statusCode: StatusCodes.Status400BadRequest,
            contentType: "application/problem+json");
    }

    public static IResult FromFailure(IReadOnlyList<ZeroError> errors)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request failed.",
            Type = "https://zerodotnet.dev/problems/request-failed",
        };

        details.Extensions["zero.errors"] = errors
            .Select(error => new
            {
                error.Code,
                error.Target,
                error.Message,
            })
            .ToArray();

        return Results.Json(
            details,
            statusCode: StatusCodes.Status400BadRequest,
            contentType: "application/problem+json");
    }
}
