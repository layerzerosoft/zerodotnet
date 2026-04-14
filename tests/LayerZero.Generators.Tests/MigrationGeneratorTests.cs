using LayerZero.Migrations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Generators.Tests;

public sealed class MigrationGeneratorTests
{
    [Fact]
    public void Generates_add_migrations_for_discovered_migrations_and_seeds()
    {
        const string source = """
            using LayerZero.Migrations;

            namespace Demo;

            public sealed class CreateAccountsMigration : Migration
            {
                public CreateAccountsMigration()
                    : base("20260414120000", "Create accounts", MigrationTransactionMode.NonTransactional)
                {
                }

                public override void Build(MigrationBuilder builder)
                {
                    builder.CreateTable("accounts", table =>
                    {
                        table.Column("id").AsGuid().NotNull();
                        table.Column("email").AsString(256).NotNull();
                        table.PrimaryKey("id");
                    });
                }
            }

            public sealed class BaselineRolesSeed : Seed
            {
                public BaselineRolesSeed()
                    : base("20260414121000", "Baseline roles")
                {
                }

                public override void Build(SeedBuilder builder)
                {
                    builder.InsertData("roles", rows =>
                    {
                        rows.Row(row => row.Set("id", 1).Set("name", "admin"));
                    });
                }
            }

            public sealed class DevRolesSeed : Seed
            {
                public DevRolesSeed()
                    : base("20260414122000", "Developer roles", "dev")
                {
                }

                public override void Build(SeedBuilder builder)
                {
                    builder.InsertData("roles", rows =>
                    {
                        rows.Row(row => row.Set("id", 2).Set("name", "developer"));
                    });
                }
            }
            """;

        var result = RunGenerator(source);
        var generated = string.Join(Environment.NewLine, result.GeneratedSources.Select(static item => item.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddMigrations", generated, StringComparison.Ordinal);
        Assert.Contains("LayerZeroGeneratedMigrationRegistry", generated, StringComparison.Ordinal);
        Assert.Contains("CreateAccountsMigration", generated, StringComparison.Ordinal);
        Assert.Contains("BaselineRolesSeed", generated, StringComparison.Ordinal);
        Assert.Contains("MigrationTransactionMode.NonTransactional", generated, StringComparison.Ordinal);
        Assert.Contains("\"dev\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_migration_ids()
    {
        const string source = """
            using LayerZero.Migrations;

            namespace Demo;

            public sealed class FirstMigration : Migration
            {
                public FirstMigration()
                    : base("20260414120000", "First")
                {
                }

                public override void Build(MigrationBuilder builder)
                {
                }
            }

            public sealed class SecondMigration : Migration
            {
                public SecondMigration()
                    : base("20260414120000", "Second")
                {
                }

                public override void Build(MigrationBuilder builder)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN009");
    }

    [Fact]
    public void Reports_duplicate_seed_ids_in_same_profile()
    {
        const string source = """
            using LayerZero.Migrations;

            namespace Demo;

            public sealed class FirstSeed : Seed
            {
                public FirstSeed()
                    : base("20260414120000", "First", "dev")
                {
                }

                public override void Build(SeedBuilder builder)
                {
                }
            }

            public sealed class SecondSeed : Seed
            {
                public SecondSeed()
                    : base("20260414120000", "Second", "dev")
                {
                }

                public override void Build(SeedBuilder builder)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN010");
    }

    [Fact]
    public void Reports_invalid_seed_profiles()
    {
        const string source = """
            using LayerZero.Migrations;

            namespace Demo;

            public sealed class InvalidSeed : Seed
            {
                public InvalidSeed()
                    : base("20260414120000", "Invalid", "Dev_Profile")
                {
                }

                public override void Build(SeedBuilder builder)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZGEN011");
    }

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default);

        var compilation = CSharpCompilation.Create(
            "LayerZero.Generator.Migrations.Tests.Input",
            [syntaxTree],
            MetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new MigrationGenerator().AsSourceGenerator());
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
            typeof(Migration).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
        };

        return platformReferences
            .Concat(projectReferences)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
