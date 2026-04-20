using LayerZero.Core;
using LayerZero.Messaging;
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
            using LayerZero.Messaging;
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

                [IdempotentMessage]
                public sealed record Archive(string Name) : ICommand;

                [IdempotentHandler]
                public sealed class ArchiveHandler : ICommandHandler<Archive>
                {
                    public ValueTask<Result> HandleAsync(Archive command, CancellationToken cancellationToken = default)
                    {
                        return ValueTask.FromResult(Result.Success());
                    }
                }
            }
            """;

        var result = RunGenerator(source);
        var generatedSources = result.GeneratedSources.Select(static source => source.SourceText.ToString()).ToArray();
        var combinedSource = string.Join(Environment.NewLine, generatedSources);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddSlices", combinedSource, StringComparison.Ordinal);
        Assert.Contains("MapSlices", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddMessages", combinedSource, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMessagingRegistrar", combinedSource, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMessageRegistry", combinedSource, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMessageJsonContext", combinedSource, StringComparison.Ordinal);
        Assert.Contains("global::Demo.AlphaSlice.MapEndpoint(endpoints);", combinedSource, StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.IAsyncRequestHandler<global::Demo.AlphaSlice.Request, string>",
            combinedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Validation.IValidator<global::Demo.AlphaSlice.Request>",
            combinedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.ICommandHandler<global::Demo.AlphaSlice.Command, string>",
            combinedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.IEventHandler<global::Demo.AlphaSlice.Published>",
            combinedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::LayerZero.Core.ICommandHandler<global::Demo.AlphaSlice.Archive>",
            combinedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiresIdempotency => true",
            combinedSource,
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

        var result = RunGenerator(source);

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

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN004");
    }

    [Fact]
    public void Reports_generated_extension_collisions()
    {
        const string source = """
            namespace LayerZero.AspNetCore;

            public static class LayerZeroGeneratedSliceExtensions_LayerZero_Generator_Tests_Input
            {
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN002");
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void Reports_duplicate_command_handlers_for_messaging()
    {
        const string source = """
            using LayerZero.Core;
            using LayerZero.Messaging;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            public sealed record Archive() : ICommand;

            public sealed class FirstArchiveHandler : ICommandHandler<Archive>
            {
                public ValueTask<Result> HandleAsync(Archive command, CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(Result.Success());
                }
            }

            public sealed class SecondArchiveHandler : ICommandHandler<Archive>
            {
                public ValueTask<Result> HandleAsync(Archive command, CancellationToken cancellationToken = default)
                {
                    return ValueTask.FromResult(Result.Success());
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN005");
    }

    [Fact]
    public void Reports_duplicate_message_names()
    {
        const string source = """
            using LayerZero.Core;
            using LayerZero.Messaging;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo
            {
                [MessageName("demo.shared")]
                public sealed record Created() : IEvent;
            }

            namespace Demo.More
            {
                [MessageName("demo.shared")]
                public sealed record CreatedAgain() : IEvent;
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN006");
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        var compilation = CSharpCompilation.Create(
            "LayerZero.Generator.Tests.Input",
            [syntaxTree],
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new SliceGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var failures = diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !diagnostic.Id.StartsWith("LZGEN", StringComparison.Ordinal))
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
            typeof(Result).Assembly.Location,
            typeof(IMessageRegistry).Assembly.Location,
            typeof(LayerZero.AspNetCore.ServiceCollectionExtensions).Assembly.Location,
            typeof(IValidator<>).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
        };

        return platformReferences
            .Concat(projectReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
