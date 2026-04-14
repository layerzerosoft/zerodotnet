using System.Text;

namespace LayerZero.Migrations;

internal sealed class MigrationScaffolder
{
    public string ScaffoldMigration(string rootPath, string rootNamespace, string name, bool nonTransactional)
    {
        var baseName = ToPascalCase(name);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var directory = Path.Combine(rootPath, "Migrations");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{timestamp}_{baseName}.cs");
        EnsureDoesNotExist(path);

        var transactionModeLine = nonTransactional
            ? """
                    /// <inheritdoc />
                    public override MigrationTransactionMode TransactionMode => MigrationTransactionMode.NonTransactional;

            """
            : string.Empty;

        var content = $$"""
            namespace {{rootNamespace}};

            internal sealed class {{baseName}}Migration : Migration
            {
            {{transactionModeLine}}    /// <inheritdoc />
                public override void Build(MigrationBuilder builder)
                {
                }
            }
            """;

        File.WriteAllText(path, Normalize(content), Encoding.UTF8);
        return path;
    }

    public string ScaffoldSeed(string rootPath, string rootNamespace, string name, string profile)
    {
        var baseName = ToPascalCase(name);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var directory = Path.Combine(rootPath, "Seeds", profile);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{timestamp}_{baseName}.cs");
        EnsureDoesNotExist(path);

        var content = $$"""
            namespace {{rootNamespace}};

            internal sealed class {{baseName}}Seed : Seed
            {
                /// <inheritdoc />
                public override void Build(SeedBuilder builder)
                {
                }
            }
            """;

        File.WriteAllText(path, Normalize(content), Encoding.UTF8);
        return path;
    }

    private static string ToPascalCase(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var parts = value
            .Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            var normalized = char.ToUpperInvariant(part[0]) + part[1..];
            builder.Append(normalized);
        }

        return builder.ToString();
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() + Environment.NewLine;
    }

    private static void EnsureDoesNotExist(string path)
    {
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"The migration artifact '{path}' already exists.");
        }
    }
}
