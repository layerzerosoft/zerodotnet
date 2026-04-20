using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Messaging;

/// <summary>
/// Runs LayerZero messaging administration commands through the application host.
/// </summary>
public static class MessagingCommandHostExtensions
{
    /// <summary>
    /// Tries to run a LayerZero messaging command.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="buildHost">Builds the configured host when a runtime command needs services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command exit code when a messaging command was handled; otherwise <see langword="null" />.</returns>
    public static async Task<int?> RunLayerZeroMessagingCommandAsync(
        this IHostApplicationBuilder builder,
        string[] args,
        Func<IHost> buildHost,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(buildHost);

        if (!MessagingCommandArguments.TryParse(args, Console.Error, out var command))
        {
            return args.Length > 0 && args[0].Equals("messaging", StringComparison.OrdinalIgnoreCase)
                ? 1
                : null;
        }

        using var host = buildHost();
        var provisioner = host.Services.GetRequiredService<IMessageTopologyProvisioner>();

        switch (command.Command)
        {
            case "validate":
                await provisioner.ValidateAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("LayerZero messaging topology validation succeeded.");
                return 0;
            case "provision":
                await provisioner.ProvisionAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("LayerZero messaging topology provisioning completed.");
                return 0;
            default:
                Console.Error.WriteLine($"Unsupported messaging command '{command.Command}'.");
                return 1;
        }
    }
}
