namespace LayerZero.Messaging;

/// <summary>
/// Provides LayerZero message naming helpers.
/// </summary>
public static class MessageNames
{
    /// <summary>
    /// Resolves the default or overridden logical name for a message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <returns>The logical message name.</returns>
    public static string For<TMessage>() => For(typeof(TMessage));

    /// <summary>
    /// Resolves the default or overridden logical name for a message type.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The logical message name.</returns>
    public static string For(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        if (Attribute.GetCustomAttribute(messageType, typeof(MessageNameAttribute)) is MessageNameAttribute attribute
            && !string.IsNullOrWhiteSpace(attribute.Name))
        {
            return attribute.Name;
        }

        return (messageType.FullName ?? messageType.Name).Replace('+', '.');
    }
}
