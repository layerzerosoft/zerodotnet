namespace LayerZero.Messaging;

/// <summary>
/// Overrides the default logical message name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MessageNameAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the overridden logical name.
    /// </summary>
    public string Name { get; } = name;
}
