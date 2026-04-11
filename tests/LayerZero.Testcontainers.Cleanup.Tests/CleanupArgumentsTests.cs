using LayerZero.Testcontainers.Cleanup;

namespace LayerZero.Testcontainers.Cleanup.Tests;

public sealed class CleanupArgumentsTests
{
    [Fact]
    public void TryParse_reads_optional_session_ids()
    {
        var stderr = new StringWriter();

        var success = CleanupArguments.TryParse(
            ["--apply", "--older-than", "30m", "--session-id", "session-a", "--session-id", "session-b"],
            stderr,
            out var parsed);

        Assert.True(success);
        Assert.Equal(CleanupMode.Apply, parsed.Mode);
        Assert.Equal(TimeSpan.FromMinutes(30), parsed.OlderThan);
        Assert.Equal(["session-a", "session-b"], parsed.SessionIds);
    }
}
