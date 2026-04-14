namespace LayerZero.Migrations;

internal sealed class MigrationCommandArguments
{
    public string Command { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? Profile { get; init; }

    public bool NonTransactional { get; init; }

    public string? ConnectionString { get; init; }

    public List<string> Profiles { get; } = [];

    public bool IncludeBaselineSeeds { get; init; }

    public MigrationScriptKind ScriptKind { get; init; } = MigrationScriptKind.Apply;

    public string? OutputPath { get; init; }

    public static bool TryParse(string[] args, TextWriter error, out MigrationCommandArguments parsed)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);

        parsed = new MigrationCommandArguments();
        if (args.Length == 0 || !args[0].Equals("migrations", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length == 1)
        {
            WriteUsage(error);
            return false;
        }

        var command = args[1];
        var isRuntimeCommand =
            command.Equals("info", StringComparison.OrdinalIgnoreCase)
            || command.Equals("validate", StringComparison.OrdinalIgnoreCase)
            || command.Equals("script", StringComparison.OrdinalIgnoreCase)
            || command.Equals("apply", StringComparison.OrdinalIgnoreCase)
            || command.Equals("baseline", StringComparison.OrdinalIgnoreCase);
        var isScaffoldCommand =
            command.Equals("add", StringComparison.OrdinalIgnoreCase)
            || command.Equals("add-seed", StringComparison.OrdinalIgnoreCase);

        if (!isRuntimeCommand && !isScaffoldCommand)
        {
            error.WriteLine($"Unknown migrations command '{command}'.");
            WriteUsage(error);
            return false;
        }

        string? name = null;
        string? profile = null;
        string? connectionString = null;
        string? outputPath = null;
        var profiles = new List<string>();
        var includeBaselineSeeds = false;
        var nonTransactional = false;
        var scriptKind = MigrationScriptKind.Apply;

        for (var index = 2; index < args.Length; index++)
        {
            if (isScaffoldCommand && name is null && !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                name = args[index];
                continue;
            }

            switch (args[index])
            {
                case "--profile":
                    if (!TryReadValue(args, ref index, "--profile", error, out var profileValue))
                    {
                        return false;
                    }

                    if (command.Equals("add-seed", StringComparison.OrdinalIgnoreCase))
                    {
                        profile = profileValue;
                    }
                    else
                    {
                        profiles.Add(profileValue);
                    }

                    break;
                case "--connection-string":
                    if (!TryReadValue(args, ref index, "--connection-string", error, out connectionString))
                    {
                        return false;
                    }

                    break;
                case "--include-baseline-seeds":
                    includeBaselineSeeds = true;
                    break;
                case "--non-transactional":
                    nonTransactional = true;
                    break;
                case "--script-kind":
                    if (!TryReadValue(args, ref index, "--script-kind", error, out var scriptKindValue))
                    {
                        return false;
                    }

                    scriptKind = scriptKindValue.Equals("baseline", StringComparison.OrdinalIgnoreCase)
                        ? MigrationScriptKind.Baseline
                        : MigrationScriptKind.Apply;
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, "--output", error, out outputPath))
                    {
                        return false;
                    }

                    break;
                default:
                    error.WriteLine($"Unknown option '{args[index]}'.");
                    WriteUsage(error);
                    return false;
            }
        }

        if (isScaffoldCommand && string.IsNullOrWhiteSpace(name))
        {
            error.WriteLine($"Command '{command}' requires a name.");
            WriteUsage(error);
            return false;
        }

        parsed = new MigrationCommandArguments
        {
            Command = command.ToLowerInvariant(),
            Name = name,
            Profile = string.IsNullOrWhiteSpace(profile) ? SeedProfiles.Baseline : profile,
            NonTransactional = nonTransactional,
            ConnectionString = connectionString,
            IncludeBaselineSeeds = includeBaselineSeeds,
            ScriptKind = scriptKind,
            OutputPath = outputPath,
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
        writer.WriteLine("  <app> migrations add <Name> [--non-transactional]");
        writer.WriteLine("  <app> migrations add-seed <Name> [--profile <value>]");
        writer.WriteLine("  <app> migrations <info|validate|script|apply|baseline> [options]");
        writer.WriteLine("Options:");
        writer.WriteLine("  --connection-string <value>     Overrides LayerZero:Data:SqlServer:ConnectionString.");
        writer.WriteLine("  --profile <value>               Seed profile. Repeat for runtime commands.");
        writer.WriteLine("  --script-kind <apply|baseline>  Script mode for the 'script' command.");
        writer.WriteLine("  --include-baseline-seeds        Include baseline seeds during baseline scripting or execution.");
        writer.WriteLine("  --non-transactional             Generate a non-transactional migration scaffold.");
        writer.WriteLine("  --output <path>                 Write script output to the provided file.");
    }
}
