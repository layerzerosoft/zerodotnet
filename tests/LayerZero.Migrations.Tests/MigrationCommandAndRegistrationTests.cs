using LayerZero.Data;
using LayerZero.Data.SqlServer;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Migrations.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.Tests;

public sealed class MigrationCommandAndRegistrationTests
{
    [Fact]
    public void Sql_server_data_provider_reads_standard_configuration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Server=(local);Database=Demo;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:DefaultSchema", "sales"),
        ]);

        builder.Services
            .AddData(data => data.UseSqlServer());

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<SqlServerDataOptions>>().Value;

        Assert.Equal("Server=(local);Database=Demo;Trusted_Connection=True;", options.ConnectionString);
        Assert.Equal("sales", options.DefaultSchema);
    }

    [Fact]
    public void Use_migrations_registers_an_empty_catalog_without_generated_di_calls()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Server=(local);Database=Demo;Trusted_Connection=True;"),
        ]);

        builder.Services
            .AddData(data =>
            {
                data.UseSqlServer();
                data.UseMigrations();
            });

        using var host = builder.Build();
        var catalog = host.Services.GetRequiredService<IMigrationCatalog>();
        _ = host.Services.GetRequiredService<IMigrationRuntime>();

        Assert.Empty(catalog.Migrations);
        Assert.Empty(catalog.Seeds);
    }

    [Fact]
    public void Use_migrations_defaults_history_schema_from_the_active_provider()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", "Server=(local);Database=Demo;Trusted_Connection=True;"),
            new KeyValuePair<string, string?>("LayerZero:Data:SqlServer:DefaultSchema", "sales"),
        ]);

        builder.Services.AddData(data =>
        {
            data.UseSqlServer();
            data.UseMigrations();
        });

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<MigrationsOptions>>().Value;

        Assert.Equal("sales", options.HistoryTableSchema);
    }

    [Fact]
    public void Scaffolder_creates_convention_named_migration_and_seed_files()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "lz-migrations-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try
        {
            var scaffolder = new MigrationScaffolder();
            var migrationPath = scaffolder.ScaffoldMigration(rootPath, "Demo.App", "create accounts", nonTransactional: true);
            var seedPath = scaffolder.ScaffoldSeed(rootPath, "Demo.App", "baseline roles", "dev");

            Assert.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", migrationPath, StringComparison.Ordinal);
            Assert.Contains($"{Path.DirectorySeparatorChar}Seeds{Path.DirectorySeparatorChar}dev{Path.DirectorySeparatorChar}", seedPath, StringComparison.Ordinal);
            Assert.Contains("CreateAccountsMigration", File.ReadAllText(migrationPath), StringComparison.Ordinal);
            Assert.Contains("MigrationTransactionMode.NonTransactional", File.ReadAllText(migrationPath), StringComparison.Ordinal);
            Assert.Contains("BaselineRolesSeed", File.ReadAllText(seedPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
