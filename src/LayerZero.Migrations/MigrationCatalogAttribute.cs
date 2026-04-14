using System.Reflection;

namespace LayerZero.Migrations;

/// <summary>
/// Declares the generated migration catalog for one application assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MigrationCatalogAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="MigrationCatalogAttribute"/>.
    /// </summary>
    /// <param name="catalogType">The generated catalog type.</param>
    public MigrationCatalogAttribute(Type catalogType)
    {
        ArgumentNullException.ThrowIfNull(catalogType);
        CatalogType = catalogType;
    }

    /// <summary>
    /// Gets the generated catalog type.
    /// </summary>
    public Type CatalogType { get; }
}

internal static class MigrationCatalogLoader
{
    public static IMigrationCatalog LoadFromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
        {
            return EmptyMigrationCatalog.Instance;
        }

        var attribute = assembly.GetCustomAttribute<MigrationCatalogAttribute>();
        if (attribute?.CatalogType is null)
        {
            return EmptyMigrationCatalog.Instance;
        }

        if (Activator.CreateInstance(attribute.CatalogType) is not IMigrationCatalog catalog)
        {
            throw new InvalidOperationException(
                $"The generated migration catalog type '{attribute.CatalogType.FullName}' could not be created.");
        }

        return catalog;
    }
}

internal sealed class EmptyMigrationCatalog : IMigrationCatalog
{
    public static EmptyMigrationCatalog Instance { get; } = new();

    public IReadOnlyList<MigrationDescriptor> Migrations { get; } = [];

    public IReadOnlyList<SeedDescriptor> Seeds { get; } = [];
}
