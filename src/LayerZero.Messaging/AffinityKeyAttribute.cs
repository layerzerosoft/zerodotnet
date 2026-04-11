namespace LayerZero.Messaging;

/// <summary>
/// Declares the message member that should be used as the default affinity key.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AffinityKeyAttribute(string memberName) : Attribute
{
    /// <summary>
    /// Gets the message member name.
    /// </summary>
    public string MemberName { get; } = memberName;
}
