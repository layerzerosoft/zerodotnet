namespace LayerZero.Validation;

/// <summary>
/// Provides contextual data to validation rules.
/// </summary>
public sealed class ValidationContext
{
    /// <summary>
    /// Initializes a new <see cref="ValidationContext"/>.
    /// </summary>
    /// <param name="services">Optional service provider for request-scoped dependencies.</param>
    /// <param name="items">Optional custom validation items.</param>
    public ValidationContext(IServiceProvider? services = null, IReadOnlyDictionary<string, object?>? items = null)
    {
        Services = services;
        Items = items ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Gets an empty validation context.
    /// </summary>
    public static ValidationContext Empty { get; } = new();

    /// <summary>
    /// Gets the optional service provider for request-scoped dependencies.
    /// </summary>
    public IServiceProvider? Services { get; }

    /// <summary>
    /// Gets custom validation items.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Items { get; }
}
