using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Configuration;

/// <summary>
/// Builds LayerZero messaging services.
/// </summary>
public sealed class MessagingBuilder
{
    internal MessagingBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Sets the logical application name.
    /// </summary>
    /// <param name="applicationName">The application name.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder WithApplicationName(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        Services.PostConfigure<MessagingOptions>(options => options.ApplicationName = applicationName);
        return this;
    }

    /// <summary>
    /// Routes one message type to a bus.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="busName">The bus name.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder Route<TMessage>(string busName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(busName);
        var messageName = MessageNames.For<TMessage>();
        Services.PostConfigure<MessagingOptions>(options => options.MessageRoutes[messageName] = busName);
        return this;
    }

    /// <summary>
    /// Configures the underlying messaging options.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder Configure(Action<MessagingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.PostConfigure(configure);
        return this;
    }
}
