using LayerZero.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Bootstrap.Messaging;

/// <summary>
/// Adds LayerZero messaging bootstrap behavior.
/// </summary>
public static class LayerZeroBootstrapBuilderExtensions
{
    /// <summary>
    /// Adds the messaging provisioning bootstrap step and command surface.
    /// </summary>
    /// <param name="builder">The bootstrap builder.</param>
    /// <returns>The current builder.</returns>
    public static LayerZero.Bootstrap.LayerZeroBootstrapBuilder AddMessagingProvisioningStep(this LayerZero.Bootstrap.LayerZeroBootstrapBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddCommandHandler(static (hostBuilder, args, buildHost, cancellationToken) =>
                hostBuilder.RunLayerZeroMessagingCommandAsync(args, buildHost, cancellationToken))
            .AddStep(
                "messaging-provisioning",
                static async (services, cancellationToken) =>
                {
                    await services.GetRequiredService<IMessageTopologyProvisioner>()
                        .ProvisionAsync(cancellationToken)
                        .ConfigureAwait(false);
                });
    }
}
