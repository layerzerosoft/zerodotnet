using LayerZero.Migrations;
using LayerZero.Migrations.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LayerZero.Migrations.Analyzers.Tests;

public sealed class MigrationCatalogGeneratorTests
{
    [Fact]
    public void Generates_catalog_from_migration_and_seed_conventions()
    {
        var result = RunGenerator(
            [
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class CreateAccountsMigration : Migration
                    {
                        public override MigrationTransactionMode TransactionMode => MigrationTransactionMode.NonTransactional;

                        public override void Build(MigrationBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Migrations/20260414120000_CreateAccounts.cs"),
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class BaselineRolesSeed : Seed
                    {
                        public override void Build(SeedBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Seeds/baseline/20260414121000_BaselineRoles.cs"),
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class DevRolesSeed : Seed
                    {
                        public override void Build(SeedBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Seeds/dev/20260414122000_DevRoles.cs"),
            ]);

        var generated = string.Join(Environment.NewLine, result.GeneratedSources.Select(static item => item.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("MigrationAssemblyRegistrarAttribute", generated, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMigrationCatalog", generated, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMigrationRegistrar", generated, StringComparison.Ordinal);
        Assert.Contains("CreateAccountsMigration", generated, StringComparison.Ordinal);
        Assert.Contains("\"Baseline Roles\"", generated, StringComparison.Ordinal);
        Assert.Contains("\"dev\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_migration_ids()
    {
        var result = RunGenerator(
            [
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class FirstMigration : Migration
                    {
                        public override void Build(MigrationBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Migrations/20260414120000_First.cs"),
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class SecondMigration : Migration
                    {
                        public override void Build(MigrationBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Migrations/20260414120000_Second.cs"),
            ]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZMIG002");
    }

    [Fact]
    public void Reports_duplicate_seed_ids_in_one_profile()
    {
        var result = RunGenerator(
            [
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class FirstSeed : Seed
                    {
                        public override void Build(SeedBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Seeds/dev/20260414120000_First.cs"),
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class SecondSeed : Seed
                    {
                        public override void Build(SeedBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Seeds/dev/20260414120000_Second.cs"),
            ]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZMIG003");
    }

    [Fact]
    public void Reports_invalid_seed_profile_folder_names()
    {
        var result = RunGenerator(
            [
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class InvalidSeed : Seed
                    {
                        public override void Build(SeedBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Seeds/Dev_Profile/20260414120000_Invalid.cs"),
            ]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZMIG004");
    }

    [Fact]
    public void Reports_type_and_file_mismatches()
    {
        var result = RunGenerator(
            [
                (
                    """
                    using LayerZero.Migrations;

                    namespace Demo;

                    public sealed class WrongNameMigration : Migration
                    {
                        public override void Build(MigrationBuilder builder)
                        {
                        }
                    }
                    """,
                    "/repo/Demo/Migrations/20260414120000_CreateAccounts.cs"),
            ]);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZMIG006");
    }

    private static GeneratorRunResult RunGenerator(IReadOnlyList<(string Source, string Path)> sources)
    {
        var syntaxTrees = sources
            .Select(static item => CSharpSyntaxTree.ParseText(item.Source, CSharpParseOptions.Default, item.Path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "LayerZero.Migrations.Analyzers.Tests.Input",
            syntaxTrees,
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new MigrationCatalogGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var failures = diagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !diagnostic.Id.StartsWith("LZMIG", StringComparison.Ordinal))
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
            typeof(Migration).Assembly.Location,
        };

        return platformReferences
            .Concat(projectReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
