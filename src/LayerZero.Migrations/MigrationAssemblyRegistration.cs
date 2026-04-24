using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Migrations;

/// <summary>
/// Marks one generated LayerZero migrations registrar on an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MigrationAssemblyRegistrarAttribute(Type registrarType) : Attribute
{
    /// <summary>
    /// Gets the generated registrar type.
    /// </summary>
    public Type RegistrarType { get; } = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
}

/// <summary>
/// Registers generated LayerZero migration catalogs for one assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMigrationAssemblyRegistrar
{
    /// <summary>
    /// Registers generated LayerZero migration catalogs.
    /// </summary>
    /// <param name="builder">The generated registration builder.</param>
    void Register(MigrationAssemblyRegistrationBuilder builder);
}

/// <summary>
/// Collects generated migration catalogs before they are applied to DI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MigrationAssemblyRegistrationBuilder
{
    private readonly Dictionary<Type, IMigrationCatalog> catalogs = new();

    /// <summary>
    /// Adds one generated migration catalog.
    /// </summary>
    /// <typeparam name="TCatalog">The catalog type.</typeparam>
    public void AddCatalog<TCatalog>()
        where TCatalog : class, IMigrationCatalog, new()
    {
        catalogs.TryAdd(typeof(TCatalog), new TCatalog());
    }

    internal void Apply(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMigrationCatalog>(
            catalogs.Count == 0
                ? EmptyMigrationCatalog.Instance
                : new CompositeMigrationCatalog(catalogs.Values));
    }
}

/// <summary>
/// Collects generated LayerZero migration registrars from loaded assemblies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MigrationAssemblyRegistrarCatalog
{
    private static readonly ConcurrentDictionary<Type, IMigrationAssemblyRegistrar> Registrars = new();

    /// <summary>
    /// Registers one generated registrar instance.
    /// </summary>
    /// <param name="registrar">The generated registrar.</param>
    public static void Register(IMigrationAssemblyRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Registrars.TryAdd(registrar.GetType(), registrar);
    }

    /// <summary>
    /// Registers one generated registrar type.
    /// </summary>
    /// <typeparam name="TRegistrar">The generated registrar type.</typeparam>
    public static void Register<TRegistrar>()
        where TRegistrar : class, IMigrationAssemblyRegistrar, new()
    {
        Registrars.GetOrAdd(typeof(TRegistrar), static _ => new TRegistrar());
    }

    internal static void Apply(IServiceCollection services, Assembly? scopeAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new MigrationAssemblyRegistrationBuilder();
        var registrations = Registrars.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        TryAddScopeRegistrar(registrations, scopeAssembly);
        var filteredRegistrations = registrations.AsEnumerable();
        if (scopeAssembly is not null)
        {
            var reachableAssemblyNames = GetReachableAssemblyNames(
                scopeAssembly,
                filteredRegistrations.Select(static pair => pair.Key.Assembly));
            filteredRegistrations = filteredRegistrations.Where(pair =>
            {
                var assemblyName = pair.Key.Assembly.GetName().Name;
                return !string.IsNullOrWhiteSpace(assemblyName)
                    && reachableAssemblyNames.Contains(assemblyName);
            });
        }

        foreach (var registrar in filteredRegistrations
            .OrderBy(static pair => pair.Key.FullName, StringComparer.Ordinal)
            .Select(static pair => pair.Value))
        {
            registrar.Register(builder);
        }

        builder.Apply(services);
    }

    private static HashSet<string> GetReachableAssemblyNames(
        Assembly scopeAssembly,
        IEnumerable<Assembly> candidateAssemblies)
    {
        var knownAssemblies = candidateAssemblies
            .Append(scopeAssembly)
            .Select(static assembly => new { Assembly = assembly, Name = assembly.GetName().Name })
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(static entry => entry.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Assembly, StringComparer.OrdinalIgnoreCase);

        var reachableAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<Assembly>();
        pending.Enqueue(scopeAssembly);

        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            var assemblyName = assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(assemblyName)
                || !reachableAssemblyNames.Add(assemblyName))
            {
                continue;
            }

            foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (!string.IsNullOrWhiteSpace(referencedAssemblyName.Name)
                    && knownAssemblies.TryGetValue(referencedAssemblyName.Name, out var referencedAssembly))
                {
                    pending.Enqueue(referencedAssembly);
                }
            }
        }

        return reachableAssemblyNames;
    }

    private static void TryAddScopeRegistrar(
        IDictionary<Type, IMigrationAssemblyRegistrar> registrations,
        Assembly? scopeAssembly)
    {
        if (scopeAssembly?.GetCustomAttribute<MigrationAssemblyRegistrarAttribute>() is not { } attribute)
        {
            return;
        }

        if (!typeof(IMigrationAssemblyRegistrar).IsAssignableFrom(attribute.RegistrarType))
        {
            throw new InvalidOperationException(
                $"Assembly '{scopeAssembly.FullName}' declared an invalid LayerZero migration registrar type '{attribute.RegistrarType.FullName}'.");
        }

        if (Activator.CreateInstance(attribute.RegistrarType) is not IMigrationAssemblyRegistrar registrar)
        {
            throw new InvalidOperationException(
                $"LayerZero migration registrar '{attribute.RegistrarType.FullName}' could not be created.");
        }

        registrations.TryAdd(attribute.RegistrarType, registrar);
    }
}

internal sealed class CompositeMigrationCatalog : IMigrationCatalog
{
    public CompositeMigrationCatalog(IEnumerable<IMigrationCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);

        Migrations = catalogs
            .SelectMany(static catalog => catalog.Migrations)
            .GroupBy(static migration => migration.Id, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static migration => migration.Id, StringComparer.Ordinal)
            .ToArray();

        Seeds = catalogs
            .SelectMany(static catalog => catalog.Seeds)
            .GroupBy(static seed => $"{seed.Profile}/{seed.Id}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static seed => seed.Profile, StringComparer.Ordinal)
            .ThenBy(static seed => seed.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MigrationDescriptor> Migrations { get; }

    public IReadOnlyList<SeedDescriptor> Seeds { get; }
}
