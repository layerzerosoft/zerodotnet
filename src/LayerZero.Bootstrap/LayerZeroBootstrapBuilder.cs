using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Bootstrap;

/// <summary>
/// Registers ordered LayerZero bootstrap steps and commands.
/// </summary>
public sealed class LayerZeroBootstrapBuilder
{
    private readonly Internal.LayerZeroBootstrapRegistry registry;

    internal LayerZeroBootstrapBuilder(
        IHostApplicationBuilder hostBuilder,
        Internal.LayerZeroBootstrapRegistry registry)
    {
        HostBuilder = hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Gets the host builder.
    /// </summary>
    public IHostApplicationBuilder HostBuilder { get; }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services => HostBuilder.Services;

    /// <summary>
    /// Adds one ordered bootstrap step.
    /// </summary>
    /// <param name="name">The logical step name.</param>
    /// <param name="execute">The step delegate.</param>
    /// <returns>The current builder.</returns>
    public LayerZeroBootstrapBuilder AddStep(
        string name,
        LayerZeroBootstrapStep execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(execute);

        registry.AddStep(name, execute);
        return this;
    }

    /// <summary>
    /// Adds one bootstrap command handler.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <returns>The current builder.</returns>
    public LayerZeroBootstrapBuilder AddCommandHandler(LayerZeroBootstrapCommand handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        registry.AddCommandHandler(handler);
        return this;
    }
}

/// <summary>
/// Represents one asynchronous LayerZero bootstrap step.
/// </summary>
/// <param name="services">The built service provider.</param>
/// <param name="cancellationToken">The cancellation token.</param>
public delegate ValueTask LayerZeroBootstrapStep(
    IServiceProvider services,
    CancellationToken cancellationToken = default);

/// <summary>
/// Represents one bootstrap command handler.
/// </summary>
/// <param name="builder">The host builder.</param>
/// <param name="args">The raw command-line arguments.</param>
/// <param name="buildHost">Builds the configured host lazily when needed.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The handled exit code, or <see langword="null"/> when the command was not handled.</returns>
public delegate Task<int?> LayerZeroBootstrapCommand(
    IHostApplicationBuilder builder,
    string[] args,
    Func<IHost> buildHost,
    CancellationToken cancellationToken = default);
