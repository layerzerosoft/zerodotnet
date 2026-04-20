using System.ComponentModel;

namespace LayerZero.Migrations;

/// <summary>
/// Marks one provider-specific LayerZero migrations registrar on an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MigrationProviderRegistrarAttribute(Type registrarType) : Attribute
{
    /// <summary>
    /// Gets the provider registrar type.
    /// </summary>
    public Type RegistrarType { get; } = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
}
