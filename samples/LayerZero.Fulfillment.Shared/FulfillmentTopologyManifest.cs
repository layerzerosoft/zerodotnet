using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentTopologyManifest : IMessageTopologyManifest
{
    private static readonly MessageTopologyDescriptor[] MessagesValue =
        new FulfillmentMessageRegistry()
            .Messages
            .Select(static descriptor => new MessageTopologyDescriptor(descriptor))
            .ToArray();

    private static readonly IReadOnlyDictionary<Type, MessageTopologyDescriptor> ByType =
        MessagesValue.ToDictionary(static descriptor => descriptor.Message.MessageType);

    private static readonly IReadOnlyDictionary<string, MessageTopologyDescriptor> ByName =
        MessagesValue.ToDictionary(static descriptor => descriptor.Message.Name, StringComparer.Ordinal);

    public IReadOnlyList<MessageTopologyDescriptor> Messages => MessagesValue;

    public bool TryGetDescriptor(Type messageType, out MessageTopologyDescriptor descriptor)
    {
        return ByType.TryGetValue(messageType, out descriptor!);
    }

    public bool TryGetDescriptor(string messageName, out MessageTopologyDescriptor descriptor)
    {
        return ByName.TryGetValue(messageName, out descriptor!);
    }
}
