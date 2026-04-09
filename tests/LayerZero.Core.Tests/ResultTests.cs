using LayerZero.Core;
using LayerZero.Testing;

namespace LayerZero.Core.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_exposes_value()
    {
        Result<string> result = Result<string>.Success("ignite");

        string value = result.Should().Succeed();

        Assert.Equal("ignite", value);
    }

    [Fact]
    public void Failure_result_keeps_errors()
    {
        Error error = new("layerzero.demo.failed", "The demo failed.", "demo");

        Result<string> result = Result<string>.Failure(error);

        Error matching = result.Should().Fail().Contain("layerzero.demo.failed", "demo");

        Assert.Equal("The demo failed.", matching.Message);
    }

    [Fact]
    public void Failed_result_must_have_errors()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Result.Failure([]));

        Assert.Contains("at least one error", exception.Message, StringComparison.Ordinal);
    }
}
