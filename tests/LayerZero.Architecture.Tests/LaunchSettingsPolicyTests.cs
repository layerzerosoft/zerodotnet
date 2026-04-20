using System.Text.Json;

namespace LayerZero.Architecture.Tests;

public sealed class LaunchSettingsPolicyTests
{
    [Fact]
    public void Fulfillment_rabbitmq_api_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertApiLaunchProfile(
            "LayerZero.Fulfillment.RabbitMq.Api",
            "http://localhost:5381",
            "https://localhost:7381;http://localhost:5381");
    }

    [Fact]
    public void Fulfillment_azure_service_bus_api_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertApiLaunchProfile(
            "LayerZero.Fulfillment.AzureServiceBus.Api",
            "http://localhost:5382",
            "https://localhost:7382;http://localhost:5382");
    }

    [Fact]
    public void Fulfillment_kafka_api_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertApiLaunchProfile(
            "LayerZero.Fulfillment.Kafka.Api",
            "http://localhost:5383",
            "https://localhost:7383;http://localhost:5383");
    }

    [Fact]
    public void Fulfillment_nats_api_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertApiLaunchProfile(
            "LayerZero.Fulfillment.Nats.Api",
            "http://localhost:5384",
            "https://localhost:7384;http://localhost:5384");
    }

    [Fact]
    public void Fulfillment_rabbitmq_apphost_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertAppHostLaunchProfile(
            "LayerZero.Fulfillment.RabbitMq.AppHost",
            "https://localhost:17134;http://localhost:15170",
            "https://localhost:21030",
            "https://localhost:22057");
    }

    [Fact]
    public void Fulfillment_azure_service_bus_apphost_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertAppHostLaunchProfile(
            "LayerZero.Fulfillment.AzureServiceBus.AppHost",
            "https://localhost:17135;http://localhost:15171",
            "https://localhost:21031",
            "https://localhost:22058");
    }

    [Fact]
    public void Fulfillment_kafka_apphost_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertAppHostLaunchProfile(
            "LayerZero.Fulfillment.Kafka.AppHost",
            "https://localhost:17136;http://localhost:15172",
            "https://localhost:21032",
            "https://localhost:22059");
    }

    [Fact]
    public void Fulfillment_nats_apphost_sample_uses_stable_supported_launch_profile_urls()
    {
        AssertAppHostLaunchProfile(
            "LayerZero.Fulfillment.Nats.AppHost",
            "https://localhost:17137;http://localhost:15173",
            "https://localhost:21033",
            "https://localhost:22060");
    }

    [Fact]
    public void Fulfillment_apphost_vscode_debug_configurations_use_official_aspire_launch_type()
    {
        AssertAppHostDebugConfiguration(
            "Aspire: Fulfillment RabbitMQ AppHost",
            "${workspaceFolder}/samples/LayerZero.Fulfillment.RabbitMq.AppHost/LayerZero.Fulfillment.RabbitMq.AppHost.csproj");
        AssertAppHostDebugConfiguration(
            "Aspire: Fulfillment Azure Service Bus AppHost",
            "${workspaceFolder}/samples/LayerZero.Fulfillment.AzureServiceBus.AppHost/LayerZero.Fulfillment.AzureServiceBus.AppHost.csproj");
        AssertAppHostDebugConfiguration(
            "Aspire: Fulfillment Kafka AppHost",
            "${workspaceFolder}/samples/LayerZero.Fulfillment.Kafka.AppHost/LayerZero.Fulfillment.Kafka.AppHost.csproj");
        AssertAppHostDebugConfiguration(
            "Aspire: Fulfillment NATS AppHost",
            "${workspaceFolder}/samples/LayerZero.Fulfillment.Nats.AppHost/LayerZero.Fulfillment.Nats.AppHost.csproj");
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

    private static void AssertAppHostLaunchProfile(
        string sampleProjectName,
        string expectedApplicationUrl,
        string expectedOtlpEndpoint,
        string expectedResourceServiceEndpoint)
    {
        var root = FindRepositoryRoot();
        var profiles = ReadProfiles(
            root,
            sampleProjectName,
            $"The {sampleProjectName} launch settings must exist.");

        var httpsProfile = profiles.GetProperty("https");

        AssertLaunchProfile(
            httpsProfile,
            expectedApplicationUrl: expectedApplicationUrl);

        AssertEnvironmentVariable(httpsProfile, "ASPNETCORE_ENVIRONMENT", "Development");
        AssertEnvironmentVariable(httpsProfile, "DOTNET_ENVIRONMENT", "Development");
        AssertEnvironmentVariable(httpsProfile, "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", expectedOtlpEndpoint);
        AssertEnvironmentVariable(httpsProfile, "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL", expectedResourceServiceEndpoint);
    }

    private static void AssertApiLaunchProfile(
        string sampleProjectName,
        string expectedHttpApplicationUrl,
        string expectedHttpsApplicationUrl)
    {
        var root = FindRepositoryRoot();
        var profiles = ReadProfiles(
            root,
            sampleProjectName,
            $"The {sampleProjectName} launch settings must exist.");

        AssertLaunchProfile(
            profiles.GetProperty("http"),
            expectedApplicationUrl: expectedHttpApplicationUrl,
            expectedLaunchUrl: "openapi/v1.json");

        AssertLaunchProfile(
            profiles.GetProperty("https"),
            expectedApplicationUrl: expectedHttpsApplicationUrl,
            expectedLaunchUrl: "openapi/v1.json");
    }

    private static void AssertAppHostDebugConfiguration(string name, string expectedProgram)
    {
        var root = FindRepositoryRoot();
        var configurations = ReadVisualStudioCodeLaunchConfigurations(
            root,
            "The workspace VS Code launch configuration must exist for deterministic AppHost debugging.");

        var appHostConfiguration = FindConfigurationByName(configurations, name);

        AssertConfigurationProperty(appHostConfiguration, "type", "aspire");
        AssertConfigurationProperty(appHostConfiguration, "request", "launch");
        AssertConfigurationProperty(appHostConfiguration, "program", expectedProgram);

        var debuggers = appHostConfiguration.GetProperty("debuggers");
        var projectDebugger = debuggers.GetProperty("project");
        Assert.False(projectDebugger.GetProperty("justMyCode").GetBoolean());
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
