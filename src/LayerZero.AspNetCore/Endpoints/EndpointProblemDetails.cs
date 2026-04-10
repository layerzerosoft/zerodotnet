using LayerZero.Core;
using LayerZero.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LayerZero.AspNetCore;

internal static class EndpointProblemDetails
{
    public static IResult FromValidation(ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);

        var details = new HttpValidationProblemDetails(errors)
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
        var details = new ProblemDetails()
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
