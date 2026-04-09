using LayerZero.ZeroDotNet.Testing;
using LayerZero.ZeroDotNet.Validation;

namespace LayerZero.ZeroDotNet.Testing.Tests;

public sealed class ZeroAssertTests
{
    [Fact]
    public void Succeeded_includes_errors_when_result_failed()
    {
        ZeroResult<string> result = ZeroResult<string>.Failure(new ZeroError("zero.assert.failed", "Assertion failed."));

        ZeroAssertionException exception = Assert.Throws<ZeroAssertionException>(() => ZeroAssert.Succeeded(result));

        Assert.Contains("Expected success", exception.Message, StringComparison.Ordinal);
        Assert.Contains("zero.assert.failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsError_returns_matching_error()
    {
        ZeroError error = new("zero.testing.match", "Matched.", "name");

        ZeroError matching = ZeroAssert.ContainsError([error], "zero.testing.match", "name");

        Assert.Same(error, matching);
    }

    [Fact]
    public void Invalid_returns_validation_failures()
    {
        ZeroValidationResult result = ZeroValidationResult.Invalid(
        [
            new ZeroValidationFailure("Name", ZeroValidationCodes.NotEmpty, "Name is required."),
        ]);

        IReadOnlyList<ZeroValidationFailure> failures = ZeroAssert.Invalid(result);

        Assert.Single(failures);
    }
}
