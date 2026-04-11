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
    /// <param name="entityName">The default logical entity name.</param>
    /// <param name="requiresIdempotency">Whether any default handler path requires idempotency.</param>
    /// <param name="affinityMemberName">The declared affinity key member name.</param>
    /// <param name="defaultAffinityKeyAccessor">The generated default affinity key accessor.</param>
    public MessageDescriptor(
        string name,
        Type messageType,
        MessageKind kind,
        JsonTypeInfo jsonTypeInfo,
        string entityName,
        bool requiresIdempotency = false,
        string? affinityMemberName = null,
        Func<object, string?>? defaultAffinityKeyAccessor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        Name = name;
        MessageType = messageType;
        Kind = kind;
        JsonTypeInfo = jsonTypeInfo;
        EntityName = entityName;
        RequiresIdempotency = requiresIdempotency;
        AffinityMemberName = string.IsNullOrWhiteSpace(affinityMemberName) ? null : affinityMemberName;
        DefaultAffinityKeyAccessor = defaultAffinityKeyAccessor;
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

    /// <summary>
    /// Gets the default entity name used by transports.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets whether any generated handler path requires idempotency.
    /// </summary>
    public bool RequiresIdempotency { get; }

    /// <summary>
    /// Gets the declared affinity member name when one exists.
    /// </summary>
    public string? AffinityMemberName { get; }

    /// <summary>
    /// Gets whether the message has a default affinity key source.
    /// </summary>
    public bool SupportsAffinity => DefaultAffinityKeyAccessor is not null;

    internal Func<object, string?>? DefaultAffinityKeyAccessor { get; }
}
