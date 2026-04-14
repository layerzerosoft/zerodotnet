namespace LayerZero.Migrations;

/// <summary>
/// Identifies a journaled migration artifact kind.
/// </summary>
public enum MigrationArtifactKind
{
    /// <summary>
    /// A schema or data migration.
    /// </summary>
    Migration = 0,

    /// <summary>
    /// A first-class seed artifact.
    /// </summary>
    Seed = 1,
}
