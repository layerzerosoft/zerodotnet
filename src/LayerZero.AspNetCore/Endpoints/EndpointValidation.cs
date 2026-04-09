using LayerZero.Validation;

namespace LayerZero.AspNetCore;

internal static class EndpointValidation
{
    public static async ValueTask<ValidationResult> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];
        ValidationContext context = new(services);

        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator
                .ValidateAsync(request, context, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsInvalid)
            {
                failures.AddRange(result.Errors);
            }
        }

        return failures.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(failures);
    }
}
