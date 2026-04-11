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
    /// Overrides the default entity name for one message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="entityName">The entity name.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder Entity<TMessage>(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        return ConfigureConventions(options => options.Entity<TMessage>(entityName));
    }

    /// <summary>
    /// Declares an affinity selector for one message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="selector">The affinity selector.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder Affinity<TMessage>(Func<TMessage, string?> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return ConfigureConventions(options => options.Affinity(selector));
    }

    /// <summary>
    /// Configures the message conventions.
    /// </summary>
    /// <param name="configure">The conventions delegate.</param>
    /// <returns>The builder.</returns>
    public MessagingBuilder ConfigureConventions(Action<MessageConventionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.PostConfigure(configure);
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
