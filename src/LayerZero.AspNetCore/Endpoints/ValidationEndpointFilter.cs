using LayerZero.Validation;
using Microsoft.AspNetCore.Http;

namespace LayerZero.AspNetCore;

/// <summary>
/// Minimal API endpoint filter that runs LayerZero validators for a request type.
/// </summary>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
public sealed class ValidationEndpointFilter<TRequest> : IEndpointFilter
{
    private readonly IEnumerable<IValidator<TRequest>> validators;

    /// <summary>
    /// Initializes a new <see cref="ValidationEndpointFilter{TRequest}"/>.
    /// </summary>
    /// <param name="validators">The registered request validators.</param>
    public ValidationEndpointFilter(IEnumerable<IValidator<TRequest>> validators)
    {
        this.validators = validators;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (object? argument in context.Arguments)
        {
            if (argument is not TRequest request)
            {
                continue;
            }

            ValidationResult validation = await EndpointValidation.ValidateAsync(
                request,
                validators,
                context.HttpContext.RequestServices,
                context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (validation.IsInvalid)
            {
                return EndpointProblemDetails.FromValidation(validation);
            }

            break;
        }

        return await next(context).ConfigureAwait(false);
    }
}
