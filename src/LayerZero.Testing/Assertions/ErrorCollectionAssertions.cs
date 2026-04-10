using LayerZero.Core;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertions for error collections.
/// </summary>
public sealed class ErrorCollectionAssertions
{
    private readonly IReadOnlyList<Error> errors;

    internal ErrorCollectionAssertions(IReadOnlyList<Error> errors)
    {
        this.errors = errors;
    }

    /// <summary>
    /// Asserts that the collection contains an error with the expected code and optional target.
    /// </summary>
    /// <param name="code">The expected error code.</param>
    /// <param name="target">The optional expected target.</param>
    /// <returns>The matching error.</returns>
    public Error Contain(string code, string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        foreach (var error in errors)
        {
            var codeMatches = StringComparer.Ordinal.Equals(error.Code, code);
            var targetMatches = target is null || StringComparer.Ordinal.Equals(error.Target, target);
            if (codeMatches && targetMatches)
            {
                return error;
            }
        }

        var targetText = target is null ? string.Empty : $" and target '{target}'";
        throw new AssertionException($"Expected error code '{code}'{targetText}, but it was not found.");
    }
}
