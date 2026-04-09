namespace LayerZero.ZeroDotNet.Validation;

/// <summary>
/// Represents the result of validating one object.
/// </summary>
public sealed class ZeroValidationResult
{
    private ZeroValidationResult(ZeroValidationFailure[] errors)
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
    public IReadOnlyList<ZeroValidationFailure> Errors { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    public static ZeroValidationResult Valid() => new([]);

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    /// <param name="errors">The validation failures.</param>
    /// <returns>An invalid validation result.</returns>
    public static ZeroValidationResult Invalid(IEnumerable<ZeroValidationFailure> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        ZeroValidationFailure[] materialized = errors.Where(error => error is not null).ToArray();
        if (materialized.Length == 0)
        {
            return Valid();
        }

        return new ZeroValidationResult(materialized);
    }

    /// <summary>
    /// Converts validation to a core result.
    /// </summary>
    /// <returns>A successful result when valid, otherwise a failed result with validation errors.</returns>
    public ZeroResult ToResult() => IsValid ? ZeroResult.Success() : ZeroResult.Failure(Errors.Select(error => error.ToError()));
}
