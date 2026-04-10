using LayerZero.Core;

namespace LayerZero.Validation;

/// <summary>
/// Represents the result of validating one object.
/// </summary>
public sealed class ValidationResult
{
    private ValidationResult(ValidationFailure[] errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets whether validation passed.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets whether validation failed.
    /// </summary>
    public bool IsInvalid => !IsValid;

    /// <summary>
    /// Gets validation failures.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    public static ValidationResult Valid() => new([]);

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    /// <param name="errors">The validation failures.</param>
    /// <returns>An invalid validation result.</returns>
    public static ValidationResult Invalid(IEnumerable<ValidationFailure> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materialized = errors.Where(error => error is not null).ToArray();
        if (materialized.Length == 0)
        {
            return Valid();
        }

        return new ValidationResult(materialized);
    }

    /// <summary>
    /// Converts validation to a core result.
    /// </summary>
    /// <returns>A successful result when valid, otherwise a failed result with validation errors.</returns>
    public Result ToResult() => IsValid ? Result.Success() : Result.Failure(Errors.Select(error => error.ToError()));
}
