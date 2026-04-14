namespace LayerZero.Migrations;

/// <summary>
/// Exposes the discovered migration and seed artifacts.
/// </summary>
public interface IMigrationCatalog
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
