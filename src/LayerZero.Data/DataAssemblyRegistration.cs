using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Data;

/// <summary>
/// Marks one generated LayerZero data registrar on an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DataAssemblyRegistrarAttribute(Type registrarType) : Attribute
{
    /// <summary>
    /// Gets the generated registrar type.
    /// </summary>
    public Type RegistrarType { get; } = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
}

/// <summary>
/// Registers generated LayerZero data maps and handlers for one assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDataAssemblyRegistrar
{
    /// <summary>
    /// Registers generated data services into the assembly registration builder.
    /// </summary>
    /// <param name="builder">The generated registration builder.</param>
    void Register(DataAssemblyRegistrationBuilder builder);
}

/// <summary>
/// Collects generated LayerZero data registrations before they are applied to DI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DataAssemblyRegistrationBuilder
{
    private readonly Dictionary<Type, Type> maps = new();
    private readonly Dictionary<Type, Type> queryHandlers = new();
    private readonly Dictionary<Type, Type> mutationHandlers = new();

    /// <summary>
    /// Adds one generated entity map registration.
    /// </summary>
    /// <typeparam name="TMap">The entity map type.</typeparam>
    public void AddEntityMap<TMap>()
        where TMap : class, IEntityMap
    {
        AddMap(typeof(TMap), ResolveEntityType(typeof(TMap)));
    }

    /// <summary>
    /// Adds one generated reusable query handler registration.
    /// </summary>
    /// <typeparam name="THandler">The query handler type.</typeparam>
    /// <typeparam name="TQuery">The query contract type.</typeparam>
    /// <typeparam name="TResult">The query result type.</typeparam>
    public void AddQueryHandler<THandler, TQuery, TResult>()
        where THandler : class, IDataQueryHandler<TQuery, TResult>
        where TQuery : IDataQuery<TResult>
    {
        AddHandler(
            queryHandlers,
            typeof(IDataQueryHandler<TQuery, TResult>),
            typeof(THandler),
            "query handler");
    }

    /// <summary>
    /// Adds one generated reusable mutation handler registration.
    /// </summary>
    /// <typeparam name="THandler">The mutation handler type.</typeparam>
    /// <typeparam name="TMutation">The mutation contract type.</typeparam>
    /// <typeparam name="TResult">The mutation result type.</typeparam>
    public void AddMutationHandler<THandler, TMutation, TResult>()
        where THandler : class, IDataMutationHandler<TMutation, TResult>
        where TMutation : IDataMutation<TResult>
    {
        AddHandler(
            mutationHandlers,
            typeof(IDataMutationHandler<TMutation, TResult>),
            typeof(THandler),
            "mutation handler");
    }

    internal void Apply(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var map in maps.OrderBy(static pair => pair.Key.FullName, StringComparer.Ordinal))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEntityMap), map.Value));
        }

        foreach (var handler in queryHandlers.OrderBy(static pair => pair.Key.FullName, StringComparer.Ordinal))
        {
            services.TryAddTransient(handler.Key, handler.Value);
        }

        foreach (var handler in mutationHandlers.OrderBy(static pair => pair.Key.FullName, StringComparer.Ordinal))
        {
            services.TryAddTransient(handler.Key, handler.Value);
        }
    }

    private void AddMap(Type mapType, Type entityType)
    {
        if (maps.TryGetValue(entityType, out var existing))
        {
            if (existing == mapType)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Multiple LayerZero data maps were registered for entity type '{entityType.FullName}'.");
        }

        maps[entityType] = mapType;
    }

    private static void AddHandler(
        IDictionary<Type, Type> registrations,
        Type serviceType,
        Type implementationType,
        string kind)
    {
        if (registrations.TryGetValue(serviceType, out var existing))
        {
            if (existing == implementationType)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Multiple LayerZero data {kind}s were registered for contract '{serviceType.FullName}'.");
        }

        registrations[serviceType] = implementationType;
    }

    private static Type ResolveEntityType(Type mapType)
    {
        for (var current = mapType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EntityMap<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        throw new InvalidOperationException($"Type '{mapType.FullName}' is not a valid LayerZero data entity map.");
    }
}

/// <summary>
/// Collects generated data registrars from loaded assemblies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DataAssemblyRegistrarCatalog
{
    private static readonly ConcurrentDictionary<Type, IDataAssemblyRegistrar> Registrars = new();

    /// <summary>
    /// Registers one generated registrar instance.
    /// </summary>
    /// <param name="registrar">The generated registrar.</param>
    public static void Register(IDataAssemblyRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Registrars.TryAdd(registrar.GetType(), registrar);
    }

    /// <summary>
    /// Registers one generated registrar type.
    /// </summary>
    /// <typeparam name="TRegistrar">The generated registrar type.</typeparam>
    public static void Register<TRegistrar>()
        where TRegistrar : class, IDataAssemblyRegistrar, new()
    {
        Registrars.GetOrAdd(typeof(TRegistrar), static _ => new TRegistrar());
    }

    internal static void Apply(IServiceCollection services, Assembly? scopeAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new DataAssemblyRegistrationBuilder();
        var registrations = Registrars.AsEnumerable();
        if (scopeAssembly is not null)
        {
            var reachableAssemblyNames = GetReachableAssemblyNames(
                scopeAssembly,
                registrations.Select(static pair => pair.Key.Assembly));
            registrations = registrations.Where(pair =>
            {
                var assemblyName = pair.Key.Assembly.GetName().Name;
                return !string.IsNullOrWhiteSpace(assemblyName)
                    && reachableAssemblyNames.Contains(assemblyName);
            });
        }

        foreach (var registrar in registrations
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
}
