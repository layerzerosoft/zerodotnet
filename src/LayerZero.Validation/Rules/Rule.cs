namespace LayerZero.Validation;

internal sealed class Rule<T>
{
    private readonly Func<T, ValidationContext, CancellationToken, ValueTask<ValidationFailure?>> validate;

    public Rule(Func<T, ValidationContext, CancellationToken, ValueTask<ValidationFailure?>> validate)
    {
        this.validate = validate;
    }

    public ValueTask<ValidationFailure?> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken) => validate(instance, context, cancellationToken);
}
