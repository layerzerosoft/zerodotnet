namespace LayerZero.Migrations;

/// <summary>
/// Defines one first-class seed artifact.
/// </summary>
public abstract class Seed
{
    /// <summary>
    /// Initializes a new <see cref="Seed"/>.
    /// </summary>
    /// <param name="id">The sortable UTC timestamp id.</param>
    /// <param name="name">The human-readable seed name.</param>
    /// <param name="profile">The seed profile name.</param>
    protected Seed(
        string id,
        string name,
        string profile = SeedProfiles.Baseline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        Id = id;
        Name = name;
        Profile = profile;
    }

    /// <summary>
    /// Gets the sortable UTC timestamp id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable seed name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the seed profile name.
    /// </summary>
    public string Profile { get; }

    /// <summary>
    /// Builds the seed operations.
    /// </summary>
    /// <param name="builder">The seed builder.</param>
    public abstract void Build(SeedBuilder builder);
}
