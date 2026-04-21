using LayerZero.Bootstrap.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Bootstrap;

/// <summary>
/// Registers and runs LayerZero bootstrap orchestration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LayerZero bootstrap orchestration.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="configure">The optional bootstrap configuration.</param>
    /// <returns>The configured bootstrap builder.</returns>
    public static LayerZeroBootstrapBuilder AddLayerZeroBootstrap(
        this IHostApplicationBuilder builder,
        Action<LayerZeroBootstrapBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var registry = EnsureBootstrapServices(builder.Services);
        var bootstrapBuilder = new LayerZeroBootstrapBuilder(builder, registry);
        configure?.Invoke(bootstrapBuilder);
        return bootstrapBuilder;
    }

    /// <summary>
    /// Tries to run a registered LayerZero bootstrap command.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The handled exit code, or <see langword="null"/> when no bootstrap command matched.</returns>
    public static async Task<int?> RunLayerZeroBootstrapCommandsAsync(
        this IHostApplicationBuilder builder,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);

        var registry = FindRegistry(builder.Services);
        if (registry is null || args.Length == 0)
        {
            return null;
        }

        IHost? host = null;

        try
        {
            IHost BuildHost()
            {
                host ??= BuildHostInstance(builder);
                return host;
            }

            foreach (var commandHandler in registry.CommandHandlers)
            {
                var exitCode = await commandHandler(builder, args, BuildHost, cancellationToken).ConfigureAwait(false);
                if (exitCode is not null)
                {
                    return exitCode.Value;
                }
            }

            return null;
        }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host?.Dispose();
            }
        }
    }

    /// <summary>
    /// Builds the host and executes registered bootstrap steps.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunLayerZeroBootstrapAsync(
        this IHostApplicationBuilder builder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);

        EnsureBootstrapServices(builder.Services);

        using var host = BuildHostInstance(builder);
        var runner = host.Services.GetRequiredService<LayerZeroBootstrapRunner>();
        return await runner.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static LayerZeroBootstrapRegistry EnsureBootstrapServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registry = FindRegistry(services);
        if (registry is null)
        {
            registry = new LayerZeroBootstrapRegistry();
            services.TryAdd(ServiceDescriptor.Singleton(registry));
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<LayerZeroBootstrapRunner>();
        return registry;
    }

    private static LayerZeroBootstrapRegistry? FindRegistry(IServiceCollection services)
    {
        return services
            .Where(static descriptor => descriptor.ServiceType == typeof(LayerZeroBootstrapRegistry))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<LayerZeroBootstrapRegistry>()
            .LastOrDefault();
    }

    private static IHost BuildHostInstance(IHostApplicationBuilder builder)
    {
        return builder switch
        {
            HostApplicationBuilder hostBuilder => hostBuilder.Build(),
            _ => throw new NotSupportedException(
                $"LayerZero bootstrap currently supports {nameof(HostApplicationBuilder)} instances. " +
                $"The supplied builder type '{builder.GetType().FullName}' cannot be built into an {nameof(IHost)}."),
        };
    }
}
