namespace LayerZero.Migrations.Runner;

internal sealed class MigrationRunnerArguments
{
    public string Command { get; init; } = string.Empty;

    public string? ConnectionString { get; init; }

    public List<string> Profiles { get; } = [];

    public bool IncludeBaselineSeeds { get; init; }

    public MigrationScriptKind ScriptKind { get; init; } = MigrationScriptKind.Apply;

    public static bool TryParse(string[] args, TextWriter error, out MigrationRunnerArguments parsed)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);

        parsed = new MigrationRunnerArguments();
        if (args.Length == 0)
        {
            WriteUsage(error);
            return false;
        }

        var command = args[0];
        if (!command.Equals("info", StringComparison.OrdinalIgnoreCase)
            && !command.Equals("validate", StringComparison.OrdinalIgnoreCase)
            && !command.Equals("script", StringComparison.OrdinalIgnoreCase)
            && !command.Equals("apply", StringComparison.OrdinalIgnoreCase)
            && !command.Equals("baseline", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"Unknown migrations command '{command}'.");
            WriteUsage(error);
            return false;
        }

        var connectionString = default(string);
        var profiles = new List<string>();
        var includeBaselineSeeds = false;
        var scriptKind = MigrationScriptKind.Apply;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--connection-string":
                    if (!TryReadValue(args, ref index, "--connection-string", error, out connectionString))
                    {
                        return false;
                    }

                    break;
                case "--profile":
                    if (!TryReadValue(args, ref index, "--profile", error, out var profile))
                    {
                        return false;
                    }

                    profiles.Add(profile);
                    break;
                case "--include-baseline-seeds":
                    includeBaselineSeeds = true;
                    break;
                case "--script-kind":
                    if (!TryReadValue(args, ref index, "--script-kind", error, out var scriptKindValue))
                    {
                        return false;
                    }

                    if (scriptKindValue.Equals("apply", StringComparison.OrdinalIgnoreCase))
                    {
                        scriptKind = MigrationScriptKind.Apply;
                        break;
                    }

                    if (scriptKindValue.Equals("baseline", StringComparison.OrdinalIgnoreCase))
                    {
                        scriptKind = MigrationScriptKind.Baseline;
                        break;
                    }

                    error.WriteLine($"Unknown script kind '{scriptKindValue}'. Use 'apply' or 'baseline'.");
                    return false;
                default:
                    error.WriteLine($"Unknown option '{args[index]}'.");
                    WriteUsage(error);
                    return false;
            }
        }

        parsed = new MigrationRunnerArguments
        {
            Command = command.ToLowerInvariant(),
            ConnectionString = connectionString,
            IncludeBaselineSeeds = includeBaselineSeeds,
            ScriptKind = scriptKind,
        };
        parsed.Profiles.AddRange(profiles);
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, string option, TextWriter error, out string value)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            error.WriteLine($"Option '{option}' requires a value.");
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project eng/LayerZero.Migrations.Runner -- <info|validate|script|apply|baseline> [options]");
        writer.WriteLine("Options:");
        writer.WriteLine("  --connection-string <value>     SQL Server connection string. Falls back to LAYERZERO_MIGRATIONS_CONNECTION_STRING.");
        writer.WriteLine("  --profile <value>               Additional seed profile. Repeat for multiple profiles.");
        writer.WriteLine("  --script-kind <apply|baseline>  Script mode for the 'script' command.");
        writer.WriteLine("  --include-baseline-seeds        Include baseline seeds during baseline scripting or execution.");
    }
}
