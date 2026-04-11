namespace LayerZero.Architecture.Tests;

public sealed class SliceModulePolicyTests
{
    [Fact]
    public void Sample_http_slice_modules_are_static_and_not_partial()
    {
        var root = FindRepositoryRoot();
        var featuresPath = Path.Combine(root.FullName, "samples", "LayerZero.Fulfillment.Api", "Features");

        foreach (var file in Directory.EnumerateFiles(featuresPath, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("MapEndpoint(", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Contains("static class", content, StringComparison.Ordinal);
            Assert.DoesNotContain("partial class", content, StringComparison.Ordinal);
            Assert.DoesNotContain("IEndpointSlice", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Documentation_describes_static_http_slice_modules()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root.FullName, "README.md"),
            Path.Combine(root.FullName, "docs", "foundation-architecture.md"),
            Path.Combine(root.FullName, "AGENTS.md"),
        ];

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("IEndpointSlice", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AddSlice<T>()", content, StringComparison.Ordinal);
        }
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
