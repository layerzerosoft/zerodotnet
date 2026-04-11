namespace LayerZero.Messaging.Configuration;

/// <summary>
/// Configures per-message messaging conventions.
/// </summary>
public sealed class MessageConventionOptions
{
    private readonly Dictionary<string, Func<object, string?>> affinitySelectors = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets explicit bus routes keyed by logical message name.
    /// </summary>
    public IDictionary<string, string> BusRoutes { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets explicit entity names keyed by logical message name.
    /// </summary>
    public IDictionary<string, string> EntityNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Routes one message type to a named bus.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="busName">The bus name.</param>
    /// <returns>The options instance.</returns>
    public MessageConventionOptions Route<TMessage>(string busName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(busName);
        BusRoutes[MessageNames.For<TMessage>()] = busName;
        return this;
    }

    /// <summary>
    /// Overrides the default entity name for one message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="entityName">The entity name.</param>
    /// <returns>The options instance.</returns>
    public MessageConventionOptions Entity<TMessage>(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        EntityNames[MessageNames.For<TMessage>()] = entityName;
        return this;
    }

    /// <summary>
    /// Declares an affinity selector for one message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="selector">The affinity selector.</param>
    /// <returns>The options instance.</returns>
    public MessageConventionOptions Affinity<TMessage>(Func<TMessage, string?> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        affinitySelectors[MessageNames.For<TMessage>()] = message => selector((TMessage)message);
        return this;
    }

    internal bool TryGetAffinitySelector(string messageName, out Func<object, string?> selector)
    {
        return affinitySelectors.TryGetValue(messageName, out selector!);
    }
}
