namespace LayerZero.Validation;

/// <summary>
/// Base class for fluent, allocation-conscious validators.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
public abstract class Validator<T> : IValidator<T>
{
    private readonly List<Rule<T>> rules = [];

    /// <summary>
    /// Validates the provided instance using an empty validation context.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    public ValueTask<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
    {
        return ValidateAsync(instance, ValidationContext.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<ValidationResult> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(context);

        List<ValidationFailure> failures = [];
        foreach (Rule<T> rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidationFailure? failure = await rule
                .ValidateAsync(instance, context, cancellationToken)
                .ConfigureAwait(false);

            if (failure is not null)
            {
                failures.Add(failure);
            }
        }

        return failures.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(failures);
    }

    /// <summary>
    /// Starts configuring validation rules for a property.
    /// </summary>
    /// <param name="propertyName">The property or parameter name.</param>
    /// <param name="accessor">The property accessor.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>A rule builder for the property.</returns>
    protected RuleBuilder<T, TProperty> RuleFor<TProperty>(string propertyName, Func<T, TProperty> accessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(accessor);

        return new RuleBuilder<T, TProperty>(propertyName, accessor, rules.Add);
    }
}
