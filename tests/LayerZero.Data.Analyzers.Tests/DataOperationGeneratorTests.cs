using LayerZero.Data.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Analyzers.Tests;

public sealed class DataOperationGeneratorTests
{
    [Fact]
    public void Generates_add_data_operations_for_maps_and_handlers()
    {
        const string source = """
            using LayerZero.Data;
            using Microsoft.Extensions.DependencyInjection;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record Account(Guid Id, string Email);

            internal sealed class AccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.ToTable("accounts");
                    builder.Property(account => account.Id).IsKeyPart();
                    builder.Property(account => account.Email).HasStringType(200).IsRequired();
                }
            }

            internal sealed record GetAccounts() : IDataQuery<int>;

            internal sealed class GetAccountsHandler : IDataQueryHandler<GetAccounts, int>
            {
                public ValueTask<int> HandleAsync(GetAccounts query, CancellationToken cancellationToken = default) => ValueTask.FromResult(42);
            }

            internal sealed record ArchiveAccounts() : IDataMutation<int>;

            internal sealed class ArchiveAccountsHandler : IDataMutationHandler<ArchiveAccounts, int>
            {
                public ValueTask<int> HandleAsync(ArchiveAccounts mutation, CancellationToken cancellationToken = default) => ValueTask.FromResult(1);
            }
            """;

        var result = RunGenerator(source, allowedCompilerErrorIds: ["CS0311"]);
        var output = string.Join(Environment.NewLine, result.GeneratedSources.Select(static generated => generated.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddDataOperations", output, StringComparison.Ordinal);
        Assert.Contains("global::LayerZero.Data.IEntityMap, global::Demo.AccountMap", output, StringComparison.Ordinal);
        Assert.Contains("global::LayerZero.Data.IDataQueryHandler<global::Demo.GetAccounts, int>", output, StringComparison.Ordinal);
        Assert.Contains("global::LayerZero.Data.IDataMutationHandler<global::Demo.ArchiveAccounts, int>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_maps()
    {
        const string source = """
            using LayerZero.Data;
            using System;

            namespace Demo;

            internal sealed record Account(Guid Id);

            internal sealed class AccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.ToTable("accounts");
                    builder.Property(account => account.Id).IsKeyPart();
                }
            }

            internal sealed class SecondAccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.ToTable("accounts");
                    builder.Property(account => account.Id).IsKeyPart();
                }
            }
            """;

        var result = RunGenerator(source, allowedCompilerErrorIds: ["CS0311"]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA002");
    }

    [Fact]
    public void Reports_invalid_query_handler_contract()
    {
        const string source = """
            using LayerZero.Data;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record BrokenQuery();

            internal sealed class BrokenHandler : IDataQueryHandler<BrokenQuery, int>
            {
                public ValueTask<int> HandleAsync(BrokenQuery query, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
            }
            """;

        var result = RunGenerator(source, "CS0311");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA003");
    }

    [Fact]
    public void Reports_invalid_mutation_handler_contract()
    {
        const string source = """
            using LayerZero.Data;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record BrokenMutation();

            internal sealed class BrokenMutationHandler : IDataMutationHandler<BrokenMutation, int>
            {
                public ValueTask<int> HandleAsync(BrokenMutation mutation, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);
            }
            """;

        var result = RunGenerator(source, "CS0311");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA004");
    }

    private static GeneratorRunResult RunGenerator(string source, params string[] allowedCompilerErrorIds)
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
            .Where(diagnostic => !allowedCompilerErrorIds.Contains(diagnostic.Id, StringComparer.Ordinal))
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
