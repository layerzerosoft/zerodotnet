using System.Reflection;
using LayerZero.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.AspNetCore;

/// <summary>
/// Adds LayerZero endpoint conventions to Minimal API route handlers.
/// </summary>
public static class RouteHandlerBuilderExtensions
{
    /// <summary>
    /// Validates a request argument before invoking the endpoint handler.
    /// </summary>
    /// <typeparam name="TRequest">The request argument type to validate.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder Validate<TRequest>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddEndpointFilterFactory((factoryContext, next) =>
            {
                int requestArgumentIndex = GetRequestArgumentIndex<TRequest>(factoryContext.MethodInfo);

                return async invocationContext =>
                {
                    if (invocationContext.Arguments.Count <= requestArgumentIndex
                        || invocationContext.Arguments[requestArgumentIndex] is not TRequest request)
                    {
                        return await next(invocationContext).ConfigureAwait(false);
                    }

                    IEnumerable<IValidator<TRequest>> validators = invocationContext.HttpContext
                        .RequestServices
                        .GetServices<IValidator<TRequest>>();

                    ValidationResult validation = await EndpointValidation.ValidateAsync(
                        request,
                        validators,
                        invocationContext.HttpContext.RequestServices,
                        invocationContext.HttpContext.RequestAborted)
                        .ConfigureAwait(false);

                    if (validation.IsInvalid)
                    {
                        return EndpointProblemDetails.FromValidation(validation);
                    }

                    return await next(invocationContext).ConfigureAwait(false);
                };
            })
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }

    private static int GetRequestArgumentIndex<TRequest>(MethodInfo methodInfo)
    {
        ParameterInfo[] parameters = methodInfo.GetParameters();

        for (int index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType == typeof(TRequest))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Endpoint validation for {typeof(TRequest).FullName} requires a route handler argument of that exact type.");
    }
}
