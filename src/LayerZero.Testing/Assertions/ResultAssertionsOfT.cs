using LayerZero.Core;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertions for typed results.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class ResultAssertions<T>
{
    private readonly Result<T> result;

    internal ResultAssertions(Result<T> result)
    {
        this.result = result;
    }

    /// <summary>
    /// Asserts that the result succeeded and returns its value.
    /// </summary>
    /// <returns>The success value.</returns>
    public T Succeed()
    {
        if (result.IsFailure)
        {
            throw new AssertionException($"Expected success, but result failed.{AssertionFormatter.FormatErrors(result.Errors)}");
        }

        return result.Value;
    }

    /// <summary>
    /// Asserts that the result failed.
    /// </summary>
    /// <returns>Assertions for the failure errors.</returns>
    public ErrorCollectionAssertions Fail()
    {
        if (result.IsSuccess)
        {
            throw new AssertionException("Expected failure, but result succeeded.");
        }

        return new ErrorCollectionAssertions(result.Errors);
    }
}
