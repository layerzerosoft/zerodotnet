using LayerZero.Migrations;
using LayerZero.Migrations.Runner;
using LayerZero.Migrations.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (!MigrationRunnerArguments.TryParse(args, Console.Error, out var parsed))
{
    return 1;
}

var connectionString = parsed.ConnectionString
    ?? Environment.GetEnvironmentVariable("LAYERZERO_MIGRATIONS_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("A SQL Server connection string is required via --connection-string or LAYERZERO_MIGRATIONS_CONNECTION_STRING.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
var migrations = builder.Services.AddLayerZeroMigrations(options =>
{
    options.Executor = "layerzero-migrations-runner";
});
migrations.AddSqlServer(options =>
{
    options.ConnectionString = connectionString;
});
migrations.Services.AddMigrations();

using var host = builder.Build();
var runtime = host.Services.GetRequiredService<IMigrationRuntime>();

switch (parsed.Command)
{
    case "info":
    {
        var result = await runtime.InfoAsync(
            new MigrationInfoOptions
            {
                Profiles = parsed.Profiles,
            });
        Console.WriteLine($"Profiles: {string.Join(", ", result.SelectedProfiles)}");
        Console.WriteLine($"History exists: {result.HistoryExists}");
        Console.WriteLine($"Has user objects: {result.HasUserObjects}");

        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Kind}:{item.Profile}:{item.Id} {(item.IsApplied ? "applied" : "pending")} {item.Name}");
        }

        return 0;
    }
    case "validate":
    {
        var result = await runtime.ValidateAsync(
            new MigrationValidationOptions
            {
                Profiles = parsed.Profiles,
            });
        if (result.IsValid)
        {
            Console.WriteLine("LayerZero migrations validation succeeded.");
            return 0;
        }

        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"{error.Code}: {error.Message}");
        }

        return 1;
    }
    case "script":
    {
        var result = await runtime.ScriptAsync(
            new MigrationScriptOptions
            {
                Kind = parsed.ScriptKind,
                Profiles = parsed.Profiles,
                IncludeBaselineSeedProfile = parsed.IncludeBaselineSeeds,
            });
        Console.WriteLine(result.Script);
        return 0;
    }
    case "apply":
    {
        var result = await runtime.ApplyAsync(
            new MigrationApplyOptions
            {
                Profiles = parsed.Profiles,
            });
        Console.WriteLine($"Applied {result.Items.Count} artifacts.");
        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Kind}:{item.Profile}:{item.Id} {item.Name}");
        }

        return 0;
    }
    case "baseline":
    {
        var result = await runtime.BaselineAsync(
            new MigrationBaselineOptions
            {
                Profiles = parsed.Profiles,
                IncludeBaselineSeedProfile = parsed.IncludeBaselineSeeds,
            });
        Console.WriteLine($"Baselined {result.Items.Count} artifacts.");
        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Kind}:{item.Profile}:{item.Id} {item.Name}");
        }

        return 0;
    }
    default:
        Console.Error.WriteLine($"Unsupported command '{parsed.Command}'.");
        return 1;
}
