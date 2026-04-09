using System.Text;
using LayerZero.ZeroDotNet.Validation;

namespace LayerZero.ZeroDotNet.Testing;

/// <summary>
/// First-party assertions for ZeroDotNet results and validators.
/// </summary>
public static class ZeroAssert
{
    /// <summary>
    /// Asserts that the result succeeded.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    public static void Succeeded(ZeroResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            throw new ZeroAssertionException($"Expected success, but result failed.{FormatErrors(result.Errors)}");
        }
    }

    /// <summary>
    /// Asserts that the result succeeded and returns its value.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The success value.</returns>
    public static T Succeeded<T>(ZeroResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            throw new ZeroAssertionException($"Expected success, but result failed.{FormatErrors(result.Errors)}");
        }

        return result.Value;
    }

    /// <summary>
    /// Asserts that the result failed and returns its errors.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The failure errors.</returns>
    public static IReadOnlyList<ZeroError> Failed(ZeroResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new ZeroAssertionException("Expected failure, but result succeeded.");
        }

        return result.Errors;
    }

    /// <summary>
    /// Asserts that the result failed and returns its errors.
    /// </summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <returns>The failure errors.</returns>
    public static IReadOnlyList<ZeroError> Failed<T>(ZeroResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new ZeroAssertionException("Expected failure, but result succeeded.");
        }

        return result.Errors;
    }

    /// <summary>
    /// Asserts that the errors contain a specific error code and optional target.
    /// </summary>
    /// <param name="errors">The errors to inspect.</param>
    /// <param name="code">The expected error code.</param>
    /// <param name="target">The optional expected target.</param>
    /// <returns>The matching error.</returns>
    public static ZeroError ContainsError(IEnumerable<ZeroError> errors, string code, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        foreach (ZeroError error in errors)
        {
            bool codeMatches = StringComparer.Ordinal.Equals(error.Code, code);
            bool targetMatches = target is null || StringComparer.Ordinal.Equals(error.Target, target);
            if (codeMatches && targetMatches)
            {
                return error;
            }
        }

        string targetText = target is null ? string.Empty : $" and target '{target}'";
        throw new ZeroAssertionException($"Expected error code '{code}'{targetText}, but it was not found.");
    }

    /// <summary>
    /// Asserts that validation succeeded.
    /// </summary>
    /// <param name="result">The validation result to inspect.</param>
    public static void Valid(ZeroValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsInvalid)
        {
            throw new ZeroAssertionException($"Expected validation to pass, but it failed.{FormatValidationErrors(result.Errors)}");
        }
    }

    /// <summary>
    /// Asserts that validation failed and returns its failures.
    /// </summary>
    /// <param name="result">The validation result to inspect.</param>
    /// <returns>The validation failures.</returns>
    public static IReadOnlyList<ZeroValidationFailure> Invalid(ZeroValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsValid)
        {
            throw new ZeroAssertionException("Expected validation to fail, but it passed.");
        }

        return result.Errors;
    }

    private static string FormatErrors(IReadOnlyList<ZeroError> errors)
    {
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (ZeroError error in errors)
        {
            builder.AppendLine().Append(" - ").Append(error);
        }

        return builder.ToString();
    }

    private static string FormatValidationErrors(IReadOnlyList<ZeroValidationFailure> errors)
    {
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (ZeroValidationFailure error in errors)
        {
            builder
                .AppendLine()
                .Append(" - ")
                .Append(error.Code)
                .Append(" (")
                .Append(error.PropertyName)
                .Append("): ")
                .Append(error.Message);
        }

        return builder.ToString();
    }
}
