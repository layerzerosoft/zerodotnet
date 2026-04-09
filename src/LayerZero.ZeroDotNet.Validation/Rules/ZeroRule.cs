namespace LayerZero.ZeroDotNet.Validation;

internal sealed class ZeroRule<T>
{
    private readonly Func<T, ZeroValidationContext, CancellationToken, ValueTask<ZeroValidationFailure?>> validate;

    public ZeroRule(Func<T, ZeroValidationContext, CancellationToken, ValueTask<ZeroValidationFailure?>> validate)
    {
        this.validate = validate;
    }

    public ValueTask<ZeroValidationFailure?> ValidateAsync(
        T instance,
        ZeroValidationContext context,
        CancellationToken cancellationToken) => validate(instance, context, cancellationToken);
}
