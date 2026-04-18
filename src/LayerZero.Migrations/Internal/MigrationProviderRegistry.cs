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

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class MigrationProviderRegistrarAttribute(Type registrarType) : Attribute
{
    public Type RegistrarType { get; } = registrarType;
}

internal interface IMigrationProviderRegistrar
{
    void Register(IServiceCollection services);
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
    public static void Apply(IServiceCollection services, string? providerAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(services);
        var providerAssembly = EnsureProviderAssemblyLoaded(providerAssemblyName);
        if (providerAssembly is null)
        {
            return;
        }

        var registrars = providerAssembly
            .GetCustomAttributes<MigrationProviderRegistrarAttribute>()
            .Select(CreateRegistrar)
            .ToArray();

        if (registrars.Length is 0)
        {
            throw new InvalidOperationException(
                $"LayerZero migrations could not find a provider registrar in assembly '{providerAssembly.GetName().Name}'.");
        }

        foreach (var registrar in registrars)
        {
            registrar.Register(services);
        }
    }

    private static Assembly? EnsureProviderAssemblyLoaded(string? providerAssemblyName)
    {
        if (string.IsNullOrWhiteSpace(providerAssemblyName))
        {
            return null;
        }

        try
        {
            return Assembly.Load(new AssemblyName(providerAssemblyName));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"LayerZero migrations could not load provider integration assembly '{providerAssemblyName}'. Ensure the matching migrations provider package is referenced.",
                exception);
        }
    }

    private static IMigrationProviderRegistrar CreateRegistrar(MigrationProviderRegistrarAttribute attribute)
    {
        if (!typeof(IMigrationProviderRegistrar).IsAssignableFrom(attribute.RegistrarType))
        {
            throw new InvalidOperationException(
                $"LayerZero migrations provider registrar type '{attribute.RegistrarType.FullName}' does not implement '{typeof(IMigrationProviderRegistrar).FullName}'.");
        }

        if (Activator.CreateInstance(attribute.RegistrarType) is not IMigrationProviderRegistrar registrar)
        {
            throw new InvalidOperationException(
                $"LayerZero migrations provider registrar type '{attribute.RegistrarType.FullName}' must be instantiable with a public or internal parameterless constructor.");
        }

        return registrar;
    }
}
