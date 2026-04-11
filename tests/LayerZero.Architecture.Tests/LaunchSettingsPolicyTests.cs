using System.Text.Json;

namespace LayerZero.Architecture.Tests;

public sealed class LaunchSettingsPolicyTests
{
    [Fact]
    public void Fulfillment_api_sample_uses_stable_supported_launch_profile_urls()
    {
        var root = FindRepositoryRoot();
        var launchSettingsPath = Path.Combine(
            root.FullName,
            "samples",
            "LayerZero.Fulfillment.Api",
            "Properties",
            "launchSettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        var profiles = document.RootElement.GetProperty("profiles");

        AssertLaunchProfile(
            profiles.GetProperty("http"),
            expectedApplicationUrl: "http://localhost:5380",
            expectedLaunchUrl: "openapi/v1.json");

        AssertLaunchProfile(
            profiles.GetProperty("https"),
            expectedApplicationUrl: "https://localhost:7380;http://localhost:5380",
            expectedLaunchUrl: "openapi/v1.json");
    }

    private static void AssertLaunchProfile(
        JsonElement profile,
        string expectedApplicationUrl,
        string expectedLaunchUrl)
    {
        var applicationUrl = profile.GetProperty("applicationUrl").GetString()
            ?? throw new InvalidOperationException("The launch profile applicationUrl must be present.");

        Assert.DoesNotContain("localhost:0", applicationUrl, StringComparison.Ordinal);
        Assert.Equal(expectedApplicationUrl, applicationUrl);

        var launchBrowser = profile.GetProperty("launchBrowser").GetBoolean();
        Assert.True(launchBrowser);

        if (launchBrowser)
        {
            Assert.All(
                applicationUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                static address => Assert.False(address.EndsWith(":0", StringComparison.Ordinal)));
        }

        var launchUrl = profile.GetProperty("launchUrl").GetString()
            ?? throw new InvalidOperationException("The launch profile launchUrl must be present.");

        Assert.Equal(expectedLaunchUrl, launchUrl);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LayerZero.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
