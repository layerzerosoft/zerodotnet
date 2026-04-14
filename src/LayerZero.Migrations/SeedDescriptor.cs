namespace LayerZero.Migrations;

/// <summary>
/// Describes one discovered seed artifact.
/// </summary>
public sealed class SeedDescriptor
{
    private readonly Func<Seed> factory;

    /// <summary>
    /// Initializes a new <see cref="SeedDescriptor"/>.
    /// </summary>
    /// <param name="id">The sortable UTC timestamp id.</param>
    /// <param name="name">The human-readable seed name.</param>
    /// <param name="profile">The seed profile name.</param>
    /// <param name="seedType">The seed CLR type.</param>
    /// <param name="factory">The seed factory.</param>
    public SeedDescriptor(
        string id,
        string name,
        string profile,
        Type seedType,
        Func<Seed> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentNullException.ThrowIfNull(seedType);
        ArgumentNullException.ThrowIfNull(factory);

        Id = id;
        Name = name;
        Profile = profile;
        SeedType = seedType;
        this.factory = factory;
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
    /// Gets the seed CLR type.
    /// </summary>
    public Type SeedType { get; }

    /// <summary>
    /// Creates a seed instance.
    /// </summary>
    /// <returns>The created seed.</returns>
    public Seed CreateInstance() => factory();
}
