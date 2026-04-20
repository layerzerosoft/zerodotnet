namespace LayerZero.Migrations;

internal sealed class EmptyMigrationCatalog : IMigrationCatalog
{
    public static EmptyMigrationCatalog Instance { get; } = new();

    public IReadOnlyList<MigrationDescriptor> Migrations { get; } = [];

    public IReadOnlyList<SeedDescriptor> Seeds { get; } = [];
}
