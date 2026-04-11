namespace LayerZero.Testcontainers.Cleanup;

internal sealed record CleanupArguments(CleanupMode Mode, TimeSpan OlderThan, IReadOnlyList<string> SessionIds)
{
    public static CleanupArguments Default { get; } = new(CleanupMode.List, TimeSpan.FromMinutes(30), Array.Empty<string>());

    public static bool TryParse(string[] args, TextWriter stderr, out CleanupArguments parsed)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stderr);

        parsed = Default;

        var mode = CleanupMode.List;
        var modeSpecified = false;
        var olderThan = Default.OlderThan;
        var sessionIds = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    WriteUsage(stderr);
                    return false;

                case "--list":
                    if (modeSpecified && mode != CleanupMode.List)
                    {
                        stderr.WriteLine("Specify only one of --list or --apply.");
                        return false;
                    }

                    mode = CleanupMode.List;
                    modeSpecified = true;
                    break;

                case "--apply":
                    if (modeSpecified && mode != CleanupMode.Apply)
                    {
                        stderr.WriteLine("Specify only one of --list or --apply.");
                        return false;
                    }

                    mode = CleanupMode.Apply;
                    modeSpecified = true;
                    break;

                case "--older-than":
                    if (index + 1 >= args.Length)
                    {
                        stderr.WriteLine("Missing value for --older-than.");
                        return false;
                    }

                    if (!CleanupDurationParser.TryParse(args[index + 1], out olderThan))
                    {
                        stderr.WriteLine($"Unsupported duration '{args[index + 1]}'. Use values like 30m, 2h, 1d, or 00:30:00.");
                        return false;
                    }

                    index++;
                    break;

                case "--session-id":
                    if (index + 1 >= args.Length)
                    {
                        stderr.WriteLine("Missing value for --session-id.");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(args[index + 1]))
                    {
                        stderr.WriteLine("Session IDs must not be empty.");
                        return false;
                    }

                    sessionIds.Add(args[index + 1]);
                    index++;
                    break;

                default:
                    stderr.WriteLine($"Unknown argument '{args[index]}'.");
                    return false;
            }
        }

        parsed = new CleanupArguments(
            mode,
            olderThan,
            sessionIds
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("LayerZero Testcontainers cleanup");
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --list");
        writer.WriteLine("  dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 30m");
        writer.WriteLine("  dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 0m --session-id <testcontainers-session-id>");
    }
}
