using LayerZero.Testing;

namespace LayerZero.Core.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_result_exposes_value()
    {
        var result = Result<string>.Success("ignite");

        var value = result.Should().Succeed();

        Assert.Equal("ignite", value);
    }

    [Fact]
    public void Failure_result_keeps_errors()
    {
        var error = new Error("layerzero.demo.failed", "The demo failed.", "demo");

        var result = Result<string>.Failure(error);

        var matching = result.Should().Fail().Contain("layerzero.demo.failed", "demo");

        Assert.Equal("The demo failed.", matching.Message);
    }

    [Fact]
    public void Failed_result_must_have_errors()
    {
        var exception = Assert.Throws<ArgumentException>(() => Result.Failure([]));

        Assert.Contains("at least one error", exception.Message, StringComparison.Ordinal);
    }
}
