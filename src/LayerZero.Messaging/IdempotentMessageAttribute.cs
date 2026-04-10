namespace LayerZero.Messaging;

/// <summary>
/// Marks a message as requiring idempotent processing.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IdempotentMessageAttribute : Attribute;
