using LayerZero.ZeroDotNet.Validation;
using Microsoft.AspNetCore.Http;

namespace LayerZero.ZeroDotNet.AspNetCore;

/// <summary>
/// Minimal API endpoint filter that runs ZeroDotNet validators for a request type.
/// </summary>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
public sealed class ZeroValidationEndpointFilter<TRequest> : IEndpointFilter
{
    private readonly IEnumerable<IZeroValidator<TRequest>> validators;

    /// <summary>
    /// Initializes a new <see cref="ZeroValidationEndpointFilter{TRequest}"/>.
    /// </summary>
    /// <param name="validators">The registered request validators.</param>
    public ZeroValidationEndpointFilter(IEnumerable<IZeroValidator<TRequest>> validators)
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

            ZeroValidationResult validation = await ZeroEndpointValidation.ValidateAsync(
                request,
                validators,
                context.HttpContext.RequestServices,
                context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (validation.IsInvalid)
            {
                return ZeroEndpointProblemDetails.FromValidation(validation);
            }

            break;
        }

        return await next(context).ConfigureAwait(false);
    }
}
