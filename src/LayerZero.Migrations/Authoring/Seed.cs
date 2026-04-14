namespace LayerZero.Migrations;

/// <summary>
/// Defines one first-class seed artifact.
/// </summary>
public abstract class Seed
{
    /// <summary>
    /// Builds the seed operations.
    /// </summary>
    /// <param name="builder">The seed builder.</param>
    public abstract void Build(SeedBuilder builder);
}
