using LayerZero.Validation;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertions for validation failure collections.
/// </summary>
public sealed class ValidationFailureCollectionAssertions
{
    private readonly IReadOnlyList<ValidationFailure> failures;

    internal ValidationFailureCollectionAssertions(IReadOnlyList<ValidationFailure> failures)
    {
        this.failures = failures;
    }

    /// <summary>
    /// Asserts that the collection contains a validation failure with the expected code and property name.
    /// </summary>
    /// <param name="code">The expected validation code.</param>
    /// <param name="propertyName">The expected property name.</param>
    /// <returns>The matching validation failure.</returns>
    public ValidationFailure Contain(string code, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        foreach (var failure in failures)
        {
            var codeMatches = StringComparer.Ordinal.Equals(failure.Code, code);
            var propertyMatches = StringComparer.Ordinal.Equals(failure.PropertyName, propertyName);
            if (codeMatches && propertyMatches)
            {
                return failure;
            }
        }

        throw new AssertionException(
            $"Expected validation failure code '{code}' for property '{propertyName}', but it was not found.");
    }
}
