using LayerZero.Validation;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertions for validation results.
/// </summary>
public sealed class ValidationResultAssertions
{
    private readonly ValidationResult result;

    internal ValidationResultAssertions(ValidationResult result)
    {
        this.result = result;
    }

    /// <summary>
    /// Asserts that validation passed.
    /// </summary>
    public void BeValid()
    {
        if (result.IsInvalid)
        {
            throw new AssertionException(
                $"Expected validation to pass, but it failed.{AssertionFormatter.FormatValidationFailures(result.Errors)}");
        }
    }

    /// <summary>
    /// Asserts that validation failed.
    /// </summary>
    /// <returns>Assertions for the validation failures.</returns>
    public ValidationFailureCollectionAssertions BeInvalid()
    {
        if (result.IsValid)
        {
            throw new AssertionException("Expected validation to fail, but it passed.");
        }

        return new ValidationFailureCollectionAssertions(result.Errors);
    }
}
