using System.Text.Json;

namespace LayerZero.Architecture.Tests;

public sealed class LaunchSettingsPolicyTests
{
    [Fact]
    public void Fulfillment_api_sample_uses_stable_supported_launch_profile_urls()
    {
        var root = FindRepositoryRoot();
        var profiles = ReadProfiles(
            root,
            "LayerZero.Fulfillment.Api",
            "The fulfillment API sample launch settings must exist.");

        AssertLaunchProfile(
            profiles.GetProperty("http"),
            expectedApplicationUrl: "http://localhost:5380",
            expectedLaunchUrl: "openapi/v1.json");

        AssertLaunchProfile(
            profiles.GetProperty("https"),
            expectedApplicationUrl: "https://localhost:7380;http://localhost:5380",
            expectedLaunchUrl: "openapi/v1.json");
    }

    [Fact]
    public void Fulfillment_apphost_sample_uses_stable_supported_launch_profile_urls()
    {
        var root = FindRepositoryRoot();
        var profiles = ReadProfiles(
            root,
            "LayerZero.Fulfillment.AppHost",
            "The fulfillment AppHost launch settings must exist.");

        var httpsProfile = profiles.GetProperty("https");

        AssertLaunchProfile(
            httpsProfile,
            expectedApplicationUrl: "https://localhost:17134;http://localhost:15170");

        AssertEnvironmentVariable(httpsProfile, "ASPNETCORE_ENVIRONMENT", "Development");
        AssertEnvironmentVariable(httpsProfile, "DOTNET_ENVIRONMENT", "Development");
        AssertEnvironmentVariable(httpsProfile, "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "https://localhost:21030");
        AssertEnvironmentVariable(httpsProfile, "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", "https://localhost:22057");
    }

    private static void AssertLaunchProfile(
        JsonElement profile,
        string expectedApplicationUrl,
        string? expectedLaunchUrl = null)
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

        if (expectedLaunchUrl is null)
        {
            return;
        }

        var launchUrl = profile.GetProperty("launchUrl").GetString()
            ?? throw new InvalidOperationException("The launch profile launchUrl must be present.");

        Assert.Equal(expectedLaunchUrl, launchUrl);
    }

    private static JsonElement ReadProfiles(DirectoryInfo root, string sampleProjectName, string missingFileMessage)
    {
        var launchSettingsPath = Path.Combine(
            root.FullName,
            "samples",
            sampleProjectName,
            "Properties",
            "launchSettings.json");

        Assert.True(File.Exists(launchSettingsPath), missingFileMessage);

        using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        return document.RootElement.GetProperty("profiles").Clone();
    }

    private static void AssertEnvironmentVariable(JsonElement profile, string name, string expectedValue)
    {
        var environmentVariables = profile.GetProperty("environmentVariables");
        var value = environmentVariables.GetProperty(name).GetString()
            ?? throw new InvalidOperationException($"The launch profile environment variable '{name}' must be present.");

        Assert.Equal(expectedValue, value);
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
