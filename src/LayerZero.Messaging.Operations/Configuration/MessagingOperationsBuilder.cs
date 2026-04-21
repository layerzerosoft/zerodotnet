using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Operations.Configuration;

/// <summary>
/// Configures LayerZero messaging operations services.
/// </summary>
public sealed class MessagingOperationsBuilder
{
    internal MessagingOperationsBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures messaging operations options.
    /// </summary>
    /// <param name="configure">The options delegate.</param>
    /// <returns>The current builder.</returns>
    public MessagingOperationsBuilder Configure(Action<MessagingOperationsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Services.PostConfigure(configure);
        return this;
    }
}
