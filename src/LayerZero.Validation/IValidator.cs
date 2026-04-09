namespace LayerZero.Validation;

/// <summary>
/// Validates a request or model.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates the provided instance.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="context">The validation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}
