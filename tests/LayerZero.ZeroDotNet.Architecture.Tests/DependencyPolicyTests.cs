using System.Xml.Linq;

namespace LayerZero.ZeroDotNet.Architecture.Tests;

public sealed class DependencyPolicyTests
{
    private static readonly string[] BannedPackages =
    [
        "MassTransit",
        "MediatR",
        "Mediator",
        "FluentValidation",
        "FluentAssertions",
        "Swashbuckle",
        "NSwag",
        "Microsoft.EntityFrameworkCore",
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

    private static DirectoryInfo FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZeroDotNet.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
