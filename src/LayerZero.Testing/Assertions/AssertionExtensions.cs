using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertion entry points for LayerZero primitives.
/// </summary>
public static class AssertionExtensions
{
    /// <summary>
    /// Starts assertions for an untyped result.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The result assertions.</returns>
    public static ResultAssertions Should(this Result result) => new(result);

    /// <summary>
    /// Starts assertions for a typed result.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The result assertions.</returns>
    public static ResultAssertions<T> Should<T>(this Result<T> result) => new(result);

    /// <summary>
    /// Starts assertions for a validation result.
    /// </summary>
    /// <param name="result">The validation result to inspect.</param>
    /// <returns>The validation result assertions.</returns>
    public static ValidationResultAssertions Should(this ValidationResult result) => new(result);
}
