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
        "RabbitMQ.Client",
        "Azure.Messaging.ServiceBus",
        "Microsoft.Azure.ServiceBus",
        "Confluent.Kafka",
        "KafkaFlow",
        "NServiceBus",
        "Rebus",
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
        DirectoryInfo root = FindRepositoryRoot();
        IEnumerable<string> packageIds = EnumeratePackageIds(root);

        string[] violations = packageIds
            .Where(IsBannedPackage)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Project_package_references_use_central_versions()
    {
        DirectoryInfo root = FindRepositoryRoot();

        string[] referencesWithVersions = Directory
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
        DirectoryInfo root = FindRepositoryRoot();

        string[] violations = Directory
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
        DirectoryInfo root = FindRepositoryRoot();
        string allowedRepositoryUrl = "https://github.com/layerzerosoft/" + "zero" + "dotnet";
        string retiredCompound = "Zero" + "Dot" + "Net";
        string retiredNamespace = "LayerZero." + retiredCompound;
        string retiredLower = "zero" + "dotnet";
        string retiredPrefix = "Zero";
        Regex retiredSymbolPattern = new(
            $@"\b(I?{retiredPrefix}[A-Z][A-Za-z0-9_]*|Add{retiredPrefix}[A-Za-z0-9_]*|Map{retiredPrefix}[A-Za-z0-9_]*)\b");
        Regex retiredWirePattern = new(@"(?<!layer)zero\.(errors|validation\.)");

        string[] violations = EnumerateNamingPolicyFiles(root)
            .SelectMany(file => GetNamingViolations(root, file, allowedRepositoryUrl, retiredCompound, retiredNamespace, retiredLower, retiredSymbolPattern, retiredWirePattern))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Repository_does_not_use_the_retired_client_generation_path()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string[] retiredPatterns =
        [
            "LayerZero.Client" + ".Generators",
            "LayerZeroApi" + "ProjectReference",
            "LayerZeroOpenApi" + "Reference",
            "TodoApiClient" + ".g.cs",
        ];

        string[] violations = EnumerateNamingPolicyFiles(root)
            .SelectMany(file => GetRetiredClientGenerationViolations(root, file, retiredPatterns))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Shared_contract_projects_do_not_reference_aspnet_core()
    {
        DirectoryInfo root = FindRepositoryRoot();

        string[] violations = Directory
            .EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => file.Contains(".Contracts", StringComparison.Ordinal))
            .SelectMany(GetAspNetCoreReferenceViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Client_sample_does_not_hardcode_todo_routes()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string clientSamplePath = Path.Combine(root.FullName, "samples", "LayerZero.MinimalApi.Client");

        string[] violations = Directory
            .EnumerateFiles(clientSamplePath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredPath(root, file))
            .Where(file => File.ReadAllText(file).Contains("/todos", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root.FullName, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumeratePackageIds(DirectoryInfo root)
    {
        foreach (string file in Directory.EnumerateFiles(root.FullName, "*.csproj", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string packageId in GetPackageIds(file, "PackageReference"))
            {
                yield return packageId;
            }
        }

        foreach (string packageId in GetPackageIds(Path.Combine(root.FullName, "Directory.Packages.props"), "PackageVersion"))
        {
            yield return packageId;
        }
    }

    private static IEnumerable<string> GetPackageIds(string file, string elementName)
    {
        XDocument document = XDocument.Load(file);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Cast<string>();
    }

    private static IEnumerable<string> GetVersionedPackageReferences(string file)
    {
        XDocument document = XDocument.Load(file);

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

    private static IEnumerable<string> EnumerateNamingPolicyFiles(DirectoryInfo root)
    {
        string[] extensions =
        [
            ".cs",
            ".csproj",
            ".props",
            ".slnx",
            ".md",
            ".mdc",
            ".yml",
            ".yaml",
        ];

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
        string relativePath = Path.GetRelativePath(root.FullName, file);
        string content = File.ReadAllText(file).Replace(allowedRepositoryUrl, string.Empty, StringComparison.Ordinal);
        string comparablePath = relativePath.Replace(allowedRepositoryUrl, string.Empty, StringComparison.Ordinal);

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

        Match symbolMatch = retiredSymbolPattern.Match(content);
        if (symbolMatch.Success)
        {
            yield return $"{relativePath}: retired symbol prefix '{symbolMatch.Value}'";
        }

        Match wireMatch = retiredWirePattern.Match(content);
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
        string relativePath = Path.GetRelativePath(root.FullName, file);
        string content = File.ReadAllText(file);

        foreach (string retiredPattern in retiredPatterns)
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
        XDocument document = XDocument.Load(file);
        string relativePath = Path.GetRelativePath(FindRepositoryRoot().FullName, file);

        foreach (XElement packageReference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
        {
            string? include = packageReference.Attribute("Include")?.Value;
            if (!string.IsNullOrWhiteSpace(include)
                && include.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            {
                yield return $"{relativePath}: ASP.NET Core package reference '{include}'";
            }
        }

        foreach (XElement projectReference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
        {
            string? include = projectReference.Attribute("Include")?.Value;
            if (!string.IsNullOrWhiteSpace(include)
                && include.Contains("AspNetCore", StringComparison.Ordinal))
            {
                yield return $"{relativePath}: ASP.NET Core project reference '{include}'";
            }
        }
    }

    private static IEnumerable<string> GetRuntimeAssemblyScanningViolations(DirectoryInfo root, string file)
    {
        string content = File.ReadAllText(file);
        string relativePath = Path.GetRelativePath(root.FullName, file);

        foreach (string pattern in RuntimeAssemblyScanningPatterns)
        {
            if (content.Contains(pattern, StringComparison.Ordinal))
            {
                yield return $"{relativePath}: runtime assembly scanning pattern '{pattern}'";
            }
        }
    }

    private static bool IsIgnoredPath(DirectoryInfo root, string file)
    {
        string relativePath = Path.GetRelativePath(root.FullName, file);
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Any(segment =>
            segment is ".git" or "bin" or "obj"
            || segment.Equals("artifacts", StringComparison.OrdinalIgnoreCase));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
