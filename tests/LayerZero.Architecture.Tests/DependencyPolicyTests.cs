using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace LayerZero.Architecture.Tests;

public sealed class DependencyPolicyTests
{
    private static readonly string[] BannedPackages =
    [
        "MassTransit",
        "MediatR",
        "Mediator",
        "FluentValidation",
        "FluentAssertions",
        "Shouldly",
        "AwesomeAssertions",
        "Swashbuckle",
        "NSwag",
        "Microsoft.Kiota",
        "Microsoft.EntityFrameworkCore",
        "KafkaFlow",
        "NServiceBus",
        "Rebus",
    ];

    private static readonly string[] BrokerPackages =
    [
        "RabbitMQ.Client",
        "Azure.Messaging.ServiceBus",
        "Microsoft.Azure.ServiceBus",
        "Confluent.Kafka",
        "NATS.Net",
    ];

    private static readonly string[] RuntimeAssemblyScanningPatterns =
    [
        "AppDomain.CurrentDomain.GetAssemblies",
        ".GetAssemblies(",
        ".GetTypes(",
        ".GetExportedTypes(",
        "Assembly.GetExecutingAssembly",
        "Assembly.Load",
    ];

    [Fact]
    public void Foundation_does_not_reference_banned_packages()
    {
        var root = FindRepositoryRoot();
        var packageIds = EnumeratePackageIds(root);

        var violations = packageIds
            .Where(IsBannedPackage)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Broker_packages_are_limited_to_messaging_adapters_samples_and_tests()
    {
        var root = FindRepositoryRoot();

        var violations = Directory
            .EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => ReferencesBrokerPackage(file))
            .Where(file => !IsBrokerPackageAllowed(file, root))
            .Select(file => Path.GetRelativePath(root.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Project_package_references_use_central_versions()
    {
        var root = FindRepositoryRoot();

        var referencesWithVersions = Directory
            .EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(GetVersionedPackageReferences)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(referencesWithVersions);
    }

    [Fact]
    public void Slice_discovery_does_not_use_runtime_assembly_scanning_by_default()
    {
        var root = FindRepositoryRoot();

        var violations = Directory
            .EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .SelectMany(file => GetRuntimeAssemblyScanningViolations(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Repository_uses_current_layerzero_naming()
    {
        var root = FindRepositoryRoot();
        var allowedRepositoryUrl = "https://github.com/layerzerosoft/" + "zero" + "dotnet";
        var retiredCompound = "Zero" + "Dot" + "Net";
        var retiredNamespace = "LayerZero." + retiredCompound;
        var retiredLower = "zero" + "dotnet";
        var retiredPrefix = "Zero";
        var retiredSymbolPattern = new Regex(
            $@"\b(I?{retiredPrefix}[A-Z][A-Za-z0-9_]*|Add{retiredPrefix}[A-Za-z0-9_]*|Map{retiredPrefix}[A-Za-z0-9_]*)\b");
        var retiredWirePattern = new Regex(@"(?<!layer)zero\.(errors|validation\.)");

        var violations = EnumerateNamingPolicyFiles(root)
            .SelectMany(file => GetNamingViolations(root, file, allowedRepositoryUrl, retiredCompound, retiredNamespace, retiredLower, retiredSymbolPattern, retiredWirePattern))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Repository_does_not_use_the_retired_client_generation_path()
    {
        var root = FindRepositoryRoot();
        var retiredPatterns = new[]
        {
            "LayerZero.Client" + ".Generators",
            "LayerZeroApi" + "ProjectReference",
            "LayerZeroOpenApi" + "Reference",
            "TodoApiClient" + ".g.cs",
        };

        var violations = EnumerateNamingPolicyFiles(root)
            .SelectMany(file => GetRetiredClientGenerationViolations(root, file, retiredPatterns))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Shared_contract_projects_do_not_reference_aspnet_core()
    {
        var root = FindRepositoryRoot();

        var violations = Directory
            .EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => file.Contains(".Contracts", StringComparison.Ordinal))
            .SelectMany(GetAspNetCoreReferenceViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Client_sample_does_not_hardcode_order_routes()
    {
        var root = FindRepositoryRoot();
        var clientSamplePath = Path.Combine(root.FullName, "samples", "LayerZero.Fulfillment.Client");

        var violations = Directory
            .EnumerateFiles(clientSamplePath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => File.ReadAllText(file).Contains("/orders", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumeratePackageIds(DirectoryInfo root)
    {
        foreach (var file in Directory.EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var packageId in GetPackageIds(file, "PackageReference"))
            {
                yield return packageId;
            }
        }

    }

    private static IEnumerable<string> GetPackageIds(string file, string elementName)
    {
        var document = XDocument.Load(file);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Cast<string>();
    }

    private static IEnumerable<string> GetVersionedPackageReferences(string file)
    {
        var document = XDocument.Load(file);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Where(element => element.Attribute("Version") is not null)
            .Select(element => $"{Path.GetRelativePath(FindRepositoryRoot().FullName, file)}: {element.Attribute("Include")?.Value}");
    }

    private static bool IsBannedPackage(string packageId)
    {
        return BannedPackages.Any(banned =>
            packageId.Equals(banned, StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith($"{banned}.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReferencesBrokerPackage(string file)
    {
        var document = XDocument.Load(file);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static packageId => !string.IsNullOrWhiteSpace(packageId))
            .Cast<string>()
            .Any(packageId => BrokerPackages.Any(brokerPackage =>
                packageId.Equals(brokerPackage, StringComparison.OrdinalIgnoreCase)
                || packageId.StartsWith($"{brokerPackage}.", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsBrokerPackageAllowed(string file, DirectoryInfo root)
    {
        var relativePath = Path.GetRelativePath(root.FullName, file);

        return relativePath.StartsWith($"src{Path.DirectorySeparatorChar}LayerZero.Messaging.", StringComparison.Ordinal)
            || relativePath.StartsWith($"samples{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateNamingPolicyFiles(DirectoryInfo root)
    {
        var extensions = new[]
        {
            ".cs",
            ".csproj",
            ".props",
            ".slnx",
            ".md",
            ".mdc",
            ".yml",
            ".yaml",
        };

        return Directory
            .EnumerateFiles(root.FullName, "*", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetNamingViolations(
        DirectoryInfo root,
        string file,
        string allowedRepositoryUrl,
        string retiredCompound,
        string retiredNamespace,
        string retiredLower,
        Regex retiredSymbolPattern,
        Regex retiredWirePattern)
    {
        var relativePath = Path.GetRelativePath(root.FullName, file);
        var content = File.ReadAllText(file).Replace(allowedRepositoryUrl, string.Empty, StringComparison.Ordinal);
        var comparablePath = relativePath.Replace(allowedRepositoryUrl, string.Empty, StringComparison.Ordinal);

        if (comparablePath.Contains(retiredCompound, StringComparison.Ordinal)
            || comparablePath.Contains(retiredLower, StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{relativePath}: retired naming in path";
        }

        if (content.Contains(retiredCompound, StringComparison.Ordinal)
            || content.Contains(retiredNamespace, StringComparison.Ordinal))
        {
            yield return $"{relativePath}: retired product or namespace";
        }

        if (content.Contains(retiredLower, StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{relativePath}: retired lowercase product name";
        }

        var symbolMatch = retiredSymbolPattern.Match(content);
        if (symbolMatch.Success)
        {
            yield return $"{relativePath}: retired symbol prefix '{symbolMatch.Value}'";
        }

        var wireMatch = retiredWirePattern.Match(content);
        if (wireMatch.Success)
        {
            yield return $"{relativePath}: retired wire name '{wireMatch.Value}'";
        }
    }

    private static IEnumerable<string> GetRetiredClientGenerationViolations(
        DirectoryInfo root,
        string file,
        string[] retiredPatterns)
    {
        var relativePath = Path.GetRelativePath(root.FullName, file);
        var content = File.ReadAllText(file);

        foreach (var retiredPattern in retiredPatterns)
        {
            if (relativePath.Contains(retiredPattern, StringComparison.Ordinal)
                || content.Contains(retiredPattern, StringComparison.Ordinal))
            {
                yield return $"{relativePath}: retired client generation marker '{retiredPattern}'";
            }
        }
    }

    private static IEnumerable<string> GetAspNetCoreReferenceViolations(string file)
    {
        var document = XDocument.Load(file);
        var relativePath = Path.GetRelativePath(FindRepositoryRoot().FullName, file);

        foreach (var packageReference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
        {
            var include = packageReference.Attribute("Include")?.Value;
            if (!string.IsNullOrWhiteSpace(include)
                && include.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            {
                yield return $"{relativePath}: ASP.NET Core package reference '{include}'";
            }
        }

        foreach (var projectReference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
        {
            var include = projectReference.Attribute("Include")?.Value;
            if (!string.IsNullOrWhiteSpace(include)
                && include.Contains("AspNetCore", StringComparison.Ordinal))
            {
                yield return $"{relativePath}: ASP.NET Core project reference '{include}'";
            }
        }
    }

    private static IEnumerable<string> GetRuntimeAssemblyScanningViolations(DirectoryInfo root, string file)
    {
        var content = File.ReadAllText(file);
        var relativePath = Path.GetRelativePath(root.FullName, file);

        foreach (var pattern in RuntimeAssemblyScanningPatterns)
        {
            if (content.Contains(pattern, StringComparison.Ordinal))
            {
                yield return $"{relativePath}: runtime assembly scanning pattern '{pattern}'";
            }
        }
    }

    private static bool IsIgnoredPath(DirectoryInfo root, string file)
    {
        var relativePath = Path.GetRelativePath(root.FullName, file);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment =>
            segment is ".git" or "bin" or "obj"
            || segment.Equals("artifacts", StringComparison.OrdinalIgnoreCase));
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
