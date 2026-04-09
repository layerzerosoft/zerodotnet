using LayerZero.ZeroDotNet.Validation;

namespace LayerZero.ZeroDotNet.AspNetCore;

internal static class ZeroEndpointValidation
{
    public static async ValueTask<ZeroValidationResult> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IZeroValidator<TRequest>> validators,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        List<ZeroValidationFailure> failures = [];
        ZeroValidationContext context = new(services);

        foreach (IZeroValidator<TRequest> validator in validators)
        {
            ZeroValidationResult result = await validator
                .ValidateAsync(request, context, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsInvalid)
            {
                failures.AddRange(result.Errors);
            }
        }

        return failures.Count == 0 ? ZeroValidationResult.Valid() : ZeroValidationResult.Invalid(failures);
    }
}
