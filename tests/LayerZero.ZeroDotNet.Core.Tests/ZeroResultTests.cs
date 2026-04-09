using LayerZero.ZeroDotNet.Testing;

namespace LayerZero.ZeroDotNet.Core.Tests;

public sealed class ZeroResultTests
{
    [Fact]
    public void Success_result_exposes_value()
    {
        ZeroResult<string> result = ZeroResult<string>.Success("ignite");

        string value = ZeroAssert.Succeeded(result);

        Assert.Equal("ignite", value);
    }

    [Fact]
    public void Failure_result_keeps_errors()
    {
        ZeroError error = new("zero.demo.failed", "The demo failed.", "demo");

        ZeroResult<string> result = ZeroResult<string>.Failure(error);

        IReadOnlyList<ZeroError> errors = ZeroAssert.Failed(result);
        ZeroError matching = ZeroAssert.ContainsError(errors, "zero.demo.failed", "demo");

        Assert.Equal("The demo failed.", matching.Message);
    }

    [Fact]
    public void Failed_result_must_have_errors()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => ZeroResult.Failure([]));

        Assert.Contains("at least one error", exception.Message, StringComparison.Ordinal);
    }
}
