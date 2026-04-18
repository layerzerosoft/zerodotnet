using LayerZero.Data.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Analyzers.Tests;

internal static class GeneratorTestHarness
{
    public static GeneratorRunResult Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);
        var compilation = CSharpCompilation.Create(
            "LayerZero.Data.Analyzers.Tests.Input",
            [syntaxTree],
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new DataOperationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var failures = diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !diagnostic.Id.StartsWith("LZDATA", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(failures);
        return Assert.Single(driver.GetRunResult().Results);
    }

    private static IReadOnlyList<MetadataReference> MetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");

        var platformReferences = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(File.Exists);

        var projectReferences = new[]
        {
            typeof(EntityMap<>).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
        };

        return platformReferences
            .Concat(projectReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
