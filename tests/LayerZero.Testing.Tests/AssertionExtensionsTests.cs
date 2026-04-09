using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Testing.Tests;

public sealed class AssertionExtensionsTests
{
    [Fact]
    public void Succeed_includes_errors_when_result_failed()
    {
        Result<string> result = Result<string>.Failure(new Error("layerzero.assert.failed", "Assertion failed."));

        AssertionException exception = Assert.Throws<AssertionException>(() => result.Should().Succeed());

        Assert.Contains("Expected success", exception.Message, StringComparison.Ordinal);
        Assert.Contains("layerzero.assert.failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_collection_assertions_return_matching_error()
    {
        Error error = new("layerzero.testing.match", "Matched.", "name");
        Result result = Result.Failure(error);

        Error matching = result.Should().Fail().Contain("layerzero.testing.match", "name");

        Assert.Same(error, matching);
    }

    [Fact]
    public void BeInvalid_returns_validation_failure_assertions()
    {
        ValidationResult result = ValidationResult.Invalid(
        [
            new ValidationFailure("Name", ValidationCodes.NotEmpty, "Name is required."),
        ]);

        ValidationFailure failure = result.Should().BeInvalid().Contain(ValidationCodes.NotEmpty, "Name");

        Assert.Equal("Name is required.", failure.Message);
    }
}
