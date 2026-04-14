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

    [Fact]
    public void Fulfillment_apphost_vscode_debug_configuration_uses_official_aspire_launch_type()
    {
        var root = FindRepositoryRoot();
        var configurations = ReadVisualStudioCodeLaunchConfigurations(
            root,
            "The workspace VS Code launch configuration must exist for deterministic AppHost debugging.");

        var appHostConfiguration = FindConfigurationByName(configurations, "Aspire: Fulfillment AppHost");

        AssertConfigurationProperty(appHostConfiguration, "type", "aspire");
        AssertConfigurationProperty(appHostConfiguration, "request", "launch");
        AssertConfigurationProperty(
            appHostConfiguration,
            "program",
            "${workspaceFolder}/samples/LayerZero.Fulfillment.AppHost/LayerZero.Fulfillment.AppHost.csproj");

        var debuggers = appHostConfiguration.GetProperty("debuggers");
        var projectDebugger = debuggers.GetProperty("project");
        Assert.False(projectDebugger.GetProperty("justMyCode").GetBoolean());
    }

    [Fact]
    public void Workspace_recommends_supported_vscode_extensions_for_aspire_debugging()
    {
        var root = FindRepositoryRoot();
        var recommendations = ReadVisualStudioCodeExtensionRecommendations(
            root,
            "The workspace must recommend the supported VS Code extensions for Aspire AppHost debugging.");

        Assert.Contains("microsoft-aspire.aspire-vscode", recommendations);
        Assert.Contains("ms-dotnettools.csdevkit", recommendations);
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

    private static JsonElement ReadVisualStudioCodeLaunchConfigurations(DirectoryInfo root, string missingFileMessage)
    {
        var launchJsonPath = Path.Combine(root.FullName, ".vscode", "launch.json");
        Assert.True(File.Exists(launchJsonPath), missingFileMessage);

        using var document = JsonDocument.Parse(File.ReadAllText(launchJsonPath));
        return document.RootElement.GetProperty("configurations").Clone();
    }

    private static string[] ReadVisualStudioCodeExtensionRecommendations(DirectoryInfo root, string missingFileMessage)
    {
        var extensionsJsonPath = Path.Combine(root.FullName, ".vscode", "extensions.json");
        Assert.True(File.Exists(extensionsJsonPath), missingFileMessage);

        using var document = JsonDocument.Parse(File.ReadAllText(extensionsJsonPath));
        return document.RootElement
            .GetProperty("recommendations")
            .EnumerateArray()
            .Select(static value => value.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static JsonElement FindConfigurationByName(JsonElement configurations, string name)
    {
        foreach (var configuration in configurations.EnumerateArray())
        {
            if (string.Equals(configuration.GetProperty("name").GetString(), name, StringComparison.Ordinal))
            {
                return configuration.Clone();
            }
        }

        throw new InvalidOperationException($"Could not find VS Code launch configuration '{name}'.");
    }

    private static void AssertEnvironmentVariable(JsonElement profile, string name, string expectedValue)
    {
        var environmentVariables = profile.GetProperty("environmentVariables");
        var value = environmentVariables.GetProperty(name).GetString()
            ?? throw new InvalidOperationException($"The launch profile environment variable '{name}' must be present.");

        Assert.Equal(expectedValue, value);
    }

    private static void AssertConfigurationProperty(JsonElement configuration, string name, string expectedValue)
    {
        var value = configuration.GetProperty(name).GetString()
            ?? throw new InvalidOperationException($"The VS Code launch configuration property '{name}' must be present.");

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
