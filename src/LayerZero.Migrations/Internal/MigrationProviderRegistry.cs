using LayerZero.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
    private static readonly Dictionary<string, Action<IServiceCollection>> Registrations = new(StringComparer.Ordinal);
    private static readonly object Sync = new();
    private static int knownProvidersLoaded;

    public static void Register(string key, Action<IServiceCollection> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(configure);

        lock (Sync)
        {
            Registrations[key] = configure;
        }
    }

    public static void Apply(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureKnownProvidersLoaded();

        KeyValuePair<string, Action<IServiceCollection>>[] registrations;
        lock (Sync)
        {
            registrations = Registrations.ToArray();
        }

        foreach (var registration in registrations)
        {
            registration.Value(services);
        }
    }

    private static void EnsureKnownProvidersLoaded()
    {
        if (Interlocked.Exchange(ref knownProvidersLoaded, 1) == 1)
        {
            return;
        }

        TryLoadOptionalIntegrationAssembly(
            "LayerZero.Migrations.SqlServer",
            "LayerZero.Migrations.SqlServer.Internal.SqlServerMigrationProviderRegistration",
            "Register");
    }

    private static void TryLoadOptionalIntegrationAssembly(string assemblyName, string registrationTypeName, string registrationMethodName)
    {
        try
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            var registrationType = assembly.GetType(registrationTypeName, throwOnError: false);
            var registrationMethod = registrationType?.GetMethod(
                registrationMethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            registrationMethod?.Invoke(obj: null, parameters: null);
        }
        catch
        {
            // Optional migration provider packages self-register when present.
        }
    }
}
