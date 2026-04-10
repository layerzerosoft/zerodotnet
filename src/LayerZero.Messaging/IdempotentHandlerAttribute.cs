namespace LayerZero.Messaging;

/// <summary>
/// Marks a message handler as requiring idempotent processing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IdempotentHandlerAttribute : Attribute;
