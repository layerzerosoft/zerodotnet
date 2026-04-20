namespace LayerZero.Messaging;

internal sealed class MessagingCommandArguments
{
    public string Command { get; init; } = string.Empty;

    public static bool TryParse(string[] args, TextWriter error, out MessagingCommandArguments parsed)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);

        parsed = new MessagingCommandArguments();
        if (args.Length == 0 || !args[0].Equals("messaging", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Length != 2)
        {
            WriteUsage(error);
            return false;
        }

        var command = args[1];
        if (!command.Equals("validate", StringComparison.OrdinalIgnoreCase)
            && !command.Equals("provision", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"Unknown messaging command '{command}'.");
            WriteUsage(error);
            return false;
        }

        parsed = new MessagingCommandArguments
        {
            Command = command.ToLowerInvariant(),
        };

        return true;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  <app> messaging <validate|provision>");
    }
}
