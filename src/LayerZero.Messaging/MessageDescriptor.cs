using System.Text.Json.Serialization.Metadata;

namespace LayerZero.Messaging;

/// <summary>
/// Describes a discovered LayerZero message.
/// </summary>
public sealed class MessageDescriptor
{
    /// <summary>
    /// Initializes a new <see cref="MessageDescriptor"/>.
    /// </summary>
    /// <param name="name">The logical message name.</param>
    /// <param name="messageType">The CLR type.</param>
    /// <param name="kind">The message kind.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type metadata.</param>
    public MessageDescriptor(
        string name,
        Type messageType,
        MessageKind kind,
        JsonTypeInfo jsonTypeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        Name = name;
        MessageType = messageType;
        Kind = kind;
        JsonTypeInfo = jsonTypeInfo;
    }

    /// <summary>
    /// Gets the logical message name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the CLR message type.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the message kind.
    /// </summary>
    public MessageKind Kind { get; }

    /// <summary>
    /// Gets source-generated JSON metadata for the message type.
    /// </summary>
    public JsonTypeInfo JsonTypeInfo { get; }
}
