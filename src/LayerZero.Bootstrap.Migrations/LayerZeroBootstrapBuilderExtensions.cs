using LayerZero.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Bootstrap.Migrations;

/// <summary>
/// Adds LayerZero migration bootstrap behavior.
/// </summary>
public static class LayerZeroBootstrapBuilderExtensions
{
    /// <summary>
    /// Adds the migrations bootstrap step and command surface.
    /// </summary>
    /// <param name="builder">The bootstrap builder.</param>
    /// <returns>The current builder.</returns>
    public static LayerZero.Bootstrap.LayerZeroBootstrapBuilder AddMigrationsStep(this LayerZero.Bootstrap.LayerZeroBootstrapBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddCommandHandler(static (hostBuilder, args, buildHost, cancellationToken) =>
                hostBuilder.RunLayerZeroMigrationsCommandAsync(args, buildHost, cancellationToken))
            .AddStep(
                "migrations",
                static async (services, cancellationToken) =>
                {
                    await services.GetRequiredService<IMigrationRuntime>()
                        .ApplyAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                });
    }
}
