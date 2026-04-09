using LayerZero.Core;
using LayerZero.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LayerZero.AspNetCore;

internal static class EndpointProblemDetails
{
    public static IResult FromValidation(ValidationResult validation)
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
            Type = "https://layerzero.dev/problems/validation",
        };

        details.Extensions["layerzero.errors"] = validation.Errors
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

    public static IResult FromFailure(IReadOnlyList<Error> errors)
    {
        ProblemDetails details = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request failed.",
            Type = "https://layerzero.dev/problems/request-failed",
        };

        details.Extensions["layerzero.errors"] = errors
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
