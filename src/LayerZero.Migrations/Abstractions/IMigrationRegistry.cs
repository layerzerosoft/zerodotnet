namespace LayerZero.Migrations;

/// <summary>
/// Exposes source-generated migration and seed descriptors.
/// </summary>
public interface IMigrationRegistry
{
    /// <summary>
    /// Gets the discovered migrations.
    /// </summary>
    IReadOnlyList<MigrationDescriptor> Migrations { get; }

    /// <summary>
    /// Gets the discovered seeds.
    /// </summary>
    IReadOnlyList<SeedDescriptor> Seeds { get; }
}
