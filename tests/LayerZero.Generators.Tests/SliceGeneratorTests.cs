using System.Collections.Immutable;
using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.Generators;
using LayerZero.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Generators.Tests;

public sealed class SliceGeneratorTests
{
    [Fact]
    public void Generates_add_slices_and_map_slices_for_discovered_types()
    {
        const string source = """
            using LayerZero.Core;
            using Microsoft.AspNetCore.Routing;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal static class AlphaSlice
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints)
                {
                }

                public sealed record Request(string? Name);

                public sealed class RequestHandler : IAsyncRequestHandler<Request, string>
                {
                    public ValueTask<Result<string>> HandleAsync(Request request, CancellationToken cancellationToken = default)
                    {
                        return ValueTask.FromResult(Result<string>.Success(request.Name ?? string.Empty));
                    }
                }

                public sealed class Validator : LayerZero.Validation.Validator<Request>
                {
                }

                public sealed record Command(string Name) : ICommand<string>;

                public sealed class CommandHandler : ICommandHandler<Command, string>
                {
                    public ValueTask<Result<string>> HandleAsync(Command command, CancellationToken cancellationToken = default)
                    {
                        return ValueTask.FromResult(Result<string>.Success(command.Name));
                    }
                }

                public sealed record Published(string Name) : IEvent;

                public sealed class PublishedHandler : IEventHandler<Published>
                {
                    public ValueTask<Result> HandleAsync(Published message, CancellationToken cancellationToken = default)
                    {
                        return ValueTask.FromResult(Result.Success());
                    }
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(source);
        string generatedSource = Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddSlices", generatedSource, StringComparison.Ordinal);
        Assert.Contains("MapSlices", generatedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.AlphaSlice.MapEndpoint(endpoints);", generatedSource, StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.IAsyncRequestHandler<global::Demo.AlphaSlice.Request, string>",
            generatedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Validation.IValidator<global::Demo.AlphaSlice.Request>",
            generatedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.ICommandHandler<global::Demo.AlphaSlice.Command, string>",
            generatedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.IEventHandler<global::Demo.AlphaSlice.Published>",
            generatedSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_invalid_endpoint_slice_shapes()
    {
        const string source = """
            using Microsoft.AspNetCore.Routing;

            namespace Demo;

            internal sealed class BrokenSlice
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints)
                {
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN001");
    }

    [Fact]
    public void Reports_partial_endpoint_slice_modules()
    {
        const string source = """
            using Microsoft.AspNetCore.Routing;

            namespace Demo;

            internal static partial class PartialSlice
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints)
                {
                }
            }
            """;

        GeneratorRunResult result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN004");
    }

    [Fact]
    public void Reports_generated_extension_collisions()
    {
        const string source = """
            namespace LayerZero.AspNetCore;

            public static class LayerZeroGeneratedSliceExtensions
            {
            }
            """;

        GeneratorRunResult result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN002");
        Assert.Empty(result.GeneratedSources);
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        CSharpCompilation compilation = CSharpCompilation.Create(
            "LayerZero.Generator.Tests.Input",
            [syntaxTree],
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SliceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> diagnostics);

        Diagnostic[] failures = diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !diagnostic.Id.StartsWith("LZGEN", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(failures);

        return Assert.Single(driver.GetRunResult().Results);
    }

    private static IReadOnlyList<MetadataReference> MetadataReferences()
    {
        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");

        IEnumerable<string> platformReferences = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(File.Exists);

        string[] projectReferences =
        [
            typeof(Result).Assembly.Location,
            typeof(ServiceCollectionExtensions).Assembly.Location,
            typeof(IValidator<>).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
        ];

        return platformReferences
            .Concat(projectReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
