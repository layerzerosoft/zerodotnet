using System.Collections.Concurrent;
using System.ComponentModel;
using LayerZero.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Migrations.Internal;

internal interface IMigrationDatabaseAdapterFactory
{
    string ProviderName { get; }

    IMigrationDatabaseAdapter Create(IServiceProvider serviceProvider);
}

internal interface IMigrationDatabaseAdapterResolver
{
    IMigrationDatabaseAdapter Resolve();
}

/// <summary>
/// Registers migrations services for one relational provider.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMigrationProviderRegistrar
{
    /// <summary>
    /// Gets the logical provider name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Registers provider-specific migrations services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    void Register(IServiceCollection services);
}

/// <summary>
/// Collects provider-specific migrations registrars from loaded assemblies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MigrationProviderRegistrarCatalog
{
    private static readonly ConcurrentDictionary<string, IMigrationProviderRegistrar> Registrars =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers one provider-specific migrations registrar.
    /// </summary>
    /// <param name="registrar">The provider registrar.</param>
    public static void Register(IMigrationProviderRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Registrars[registrar.ProviderName] = registrar;
    }

    /// <summary>
    /// Registers one provider-specific migrations registrar type.
    /// </summary>
    /// <typeparam name="TRegistrar">The provider registrar type.</typeparam>
    public static void Register<TRegistrar>()
        where TRegistrar : class, IMigrationProviderRegistrar, new()
    {
        Register(new TRegistrar());
    }

    internal static bool TryGet(string providerName, out IMigrationProviderRegistrar registrar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return Registrars.TryGetValue(providerName, out registrar!);
    }
}

internal sealed class MigrationDatabaseAdapterResolver(
    IEnumerable<IMigrationDatabaseAdapterFactory> factories,
    IDatabaseConnectionFactory connectionFactory,
    IServiceProvider serviceProvider) : IMigrationDatabaseAdapterResolver
{
    private readonly IReadOnlyList<IMigrationDatabaseAdapterFactory> factories = factories.ToArray();
    private readonly IDatabaseConnectionFactory connectionFactory = connectionFactory;
    private readonly IServiceProvider serviceProvider = serviceProvider;

    public IMigrationDatabaseAdapter Resolve()
    {
        var providerName = connectionFactory.ProviderName;
        var factory = factories.FirstOrDefault(candidate =>
            candidate.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (factory is null)
        {
            throw new InvalidOperationException(
                $"LayerZero migrations could not find a registered adapter for provider '{providerName}'.");
        }

        return factory.Create(serviceProvider);
    }
}

internal static class MigrationProviderRegistry
{
    public static void Apply(IServiceCollection services, string providerName, string? migrationsAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        if (!MigrationProviderRegistrarCatalog.TryGet(providerName, out var registrar))
        {
            throw new InvalidOperationException(
                $"LayerZero migrations could not find a registered provider integration for '{providerName}'. Ensure the matching LayerZero.Migrations provider package is referenced.");
        }

        registrar.Register(services);
    }
}
