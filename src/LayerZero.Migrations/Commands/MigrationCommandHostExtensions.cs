using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Migrations;

/// <summary>
/// Runs LayerZero migration commands through the application host.
/// </summary>
public static class MigrationCommandHostExtensions
{
    /// <summary>
    /// Tries to run a LayerZero migrations command.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="buildHost">Builds the configured host when a runtime command needs services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The command exit code when a migrations command was handled; otherwise <see langword="null" />.
    /// </returns>
    public static async Task<int?> RunLayerZeroMigrationsCommandAsync(
        this IHostApplicationBuilder builder,
        string[] args,
        Func<IHost> buildHost,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(buildHost);

        if (!MigrationCommandArguments.TryParse(args, Console.Error, out var command))
        {
            return args.Length > 0 && args[0].Equals("migrations", StringComparison.OrdinalIgnoreCase)
                ? 1
                : null;
        }

        if (!string.IsNullOrWhiteSpace(command.ConnectionString))
        {
            builder.Configuration["LayerZero:Data:ConnectionString"] = command.ConnectionString;
        }

        if (command.Command.Equals("add", StringComparison.Ordinal))
        {
            var path = new MigrationScaffolder().ScaffoldMigration(
                builder.Environment.ContentRootPath,
                builder.Environment.ApplicationName,
                command.Name!,
                command.NonTransactional);
            Console.WriteLine(path);
            return 0;
        }

        if (command.Command.Equals("add-seed", StringComparison.Ordinal))
        {
            var path = new MigrationScaffolder().ScaffoldSeed(
                builder.Environment.ContentRootPath,
                builder.Environment.ApplicationName,
                command.Name!,
                command.Profile ?? SeedProfiles.Baseline);
            Console.WriteLine(path);
            return 0;
        }

        using var host = buildHost();
        var runtime = host.Services.GetRequiredService<IMigrationRuntime>();

        switch (command.Command)
        {
            case "info":
            {
                var result = await runtime.InfoAsync(
                    new MigrationInfoOptions
                    {
                        Profiles = command.Profiles,
                    },
                    cancellationToken).ConfigureAwait(false);
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
                        Profiles = command.Profiles,
                    },
                    cancellationToken).ConfigureAwait(false);
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
                        Kind = command.ScriptKind,
                        Profiles = command.Profiles,
                        IncludeBaselineSeedProfile = command.IncludeBaselineSeeds,
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(command.OutputPath))
                {
                    await File.WriteAllTextAsync(command.OutputPath, result.Script, cancellationToken).ConfigureAwait(false);
                    Console.WriteLine(command.OutputPath);
                }
                else
                {
                    Console.WriteLine(result.Script);
                }

                return 0;
            }
            case "apply":
            {
                var result = await runtime.ApplyAsync(
                    new MigrationApplyOptions
                    {
                        Profiles = command.Profiles,
                    },
                    cancellationToken).ConfigureAwait(false);
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
                        Profiles = command.Profiles,
                        IncludeBaselineSeedProfile = command.IncludeBaselineSeeds,
                    },
                    cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"Baselined {result.Items.Count} artifacts.");
                foreach (var item in result.Items)
                {
                    Console.WriteLine($"{item.Kind}:{item.Profile}:{item.Id} {item.Name}");
                }

                return 0;
            }
            default:
                Console.Error.WriteLine($"Unsupported migrations command '{command.Command}'.");
                return 1;
        }
    }
}
