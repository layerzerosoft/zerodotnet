using LayerZero.Core;

namespace LayerZero.Testing;

/// <summary>
/// Fluent assertions for untyped results.
/// </summary>
public sealed class ResultAssertions
{
    private readonly Result result;

    internal ResultAssertions(Result result)
    {
        this.result = result;
    }

    /// <summary>
    /// Asserts that the result succeeded.
    /// </summary>
    public void Succeed()
    {
        if (result.IsFailure)
        {
            throw new AssertionException($"Expected success, but result failed.{AssertionFormatter.FormatErrors(result.Errors)}");
        }
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
