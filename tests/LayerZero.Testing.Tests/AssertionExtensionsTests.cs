using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Testing.Tests;

public sealed class AssertionExtensionsTests
{
    [Fact]
    public void Succeed_includes_errors_when_result_failed()
    {
        var result = Result<string>.Failure(new Error("layerzero.assert.failed", "Assertion failed."));

        var exception = Assert.Throws<AssertionException>(() => result.Should().Succeed());

        Assert.Contains("Expected success", exception.Message, StringComparison.Ordinal);
        Assert.Contains("layerzero.assert.failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_collection_assertions_return_matching_error()
    {
        var error = new Error("layerzero.testing.match", "Matched.", "name");
        var result = Result.Failure(error);

        var matching = result.Should().Fail().Contain("layerzero.testing.match", "name");

        Assert.Same(error, matching);
    }

    [Fact]
    public void BeInvalid_returns_validation_failure_assertions()
    {
        var result = ValidationResult.Invalid(
        [
            new ValidationFailure("Name", ValidationCodes.NotEmpty, "Name is required."),
        ]);

        var failure = result.Should().BeInvalid().Contain(ValidationCodes.NotEmpty, "Name");

        Assert.Equal("Name is required.", failure.Message);
    }
}
