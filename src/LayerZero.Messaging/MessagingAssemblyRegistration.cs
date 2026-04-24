using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Messaging;

/// <summary>
/// Marks one generated LayerZero messaging registrar on an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessagingAssemblyRegistrarAttribute(Type registrarType) : Attribute
{
    /// <summary>
    /// Gets the generated registrar type.
    /// </summary>
    public Type RegistrarType { get; } = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
}

/// <summary>
/// Registers generated LayerZero messaging services for one assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMessagingAssemblyRegistrar
{
    /// <summary>
    /// Registers generated LayerZero messaging services.
    /// </summary>
    /// <param name="builder">The generated registration builder.</param>
    void Register(MessagingAssemblyRegistrationBuilder builder);
}

/// <summary>
/// Collects generated LayerZero messaging registrations before they are applied to DI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MessagingAssemblyRegistrationBuilder
{
    private readonly List<ServiceRegistration> registrations = [];
    private readonly Dictionary<Type, IMessageRegistry> registries = new();
    private readonly Dictionary<Type, IMessageTopologyManifest> manifests = new();
    private readonly Dictionary<Type, Type> invokers = new();

    /// <summary>
    /// Adds one generated service registration.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="kind">The registration kind.</param>
    public void AddService<TService, TImplementation>(MessagingRegistrationKind kind)
        where TImplementation : class, TService
    {
        registrations.Add(new ServiceRegistration(typeof(TService), typeof(TImplementation), kind));
    }

    /// <summary>
    /// Adds one generated message registry.
    /// </summary>
    /// <typeparam name="TRegistry">The registry type.</typeparam>
    public void AddRegistry<TRegistry>()
        where TRegistry : class, IMessageRegistry, new()
    {
        registries.TryAdd(typeof(TRegistry), new TRegistry());
    }

    /// <summary>
    /// Adds one generated topology manifest.
    /// </summary>
    /// <typeparam name="TManifest">The manifest type.</typeparam>
    public void AddTopologyManifest<TManifest>()
        where TManifest : class, IMessageTopologyManifest, new()
    {
        manifests.TryAdd(typeof(TManifest), new TManifest());
    }

    /// <summary>
    /// Adds one generated handler invoker.
    /// </summary>
    /// <typeparam name="TInvoker">The invoker type.</typeparam>
    public void AddInvoker<TInvoker>()
        where TInvoker : class, IMessageHandlerInvoker
    {
        invokers.TryAdd(typeof(TInvoker), typeof(TInvoker));
    }

    internal void Apply(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var registration in registrations
            .OrderBy(static registration => registration.ServiceType.FullName, StringComparer.Ordinal)
            .ThenBy(static registration => registration.ImplementationType.FullName, StringComparer.Ordinal)
            .ThenBy(static registration => registration.Kind))
        {
            var descriptor = ServiceDescriptor.Scoped(registration.ServiceType, registration.ImplementationType);
            if (registration.Kind == MessagingRegistrationKind.TryAdd)
            {
                services.TryAdd(descriptor);
            }
            else
            {
                services.TryAddEnumerable(descriptor);
            }
        }

        IMessageRegistry registry = registries.Count == 0
            ? EmptyMessageRegistry.Instance
            : new CompositeMessageRegistry(registries.Values);
        IMessageTopologyManifest manifest = manifests.Count == 0
            ? new CompositeMessageTopologyManifest([])
            : new CompositeMessageTopologyManifest(manifests.Values);

        services.TryAddSingleton<IMessageRegistry>(registry);
        services.TryAddSingleton<IMessageTopologyManifest>(manifest);

        foreach (var invokerType in invokers.Keys.OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IMessageHandlerInvoker), invokerType));
        }
    }

    private sealed record ServiceRegistration(
        Type ServiceType,
        Type ImplementationType,
        MessagingRegistrationKind Kind);
}

/// <summary>
/// Identifies one generated DI registration behavior for messaging.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum MessagingRegistrationKind
{
    /// <summary>
    /// Registers the implementation only when the service is not already present.
    /// </summary>
    TryAdd = 0,

    /// <summary>
    /// Registers the implementation into a multi-registration service.
    /// </summary>
    TryAddEnumerable = 1,
}

/// <summary>
/// Collects generated LayerZero messaging registrars from loaded assemblies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class MessagingAssemblyRegistrarCatalog
{
    private static readonly ConcurrentDictionary<Type, IMessagingAssemblyRegistrar> Registrars = new();

    /// <summary>
    /// Registers one generated registrar instance.
    /// </summary>
    /// <param name="registrar">The generated registrar.</param>
    public static void Register(IMessagingAssemblyRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Registrars.TryAdd(registrar.GetType(), registrar);
    }

    /// <summary>
    /// Registers one generated registrar type.
    /// </summary>
    /// <typeparam name="TRegistrar">The generated registrar type.</typeparam>
    public static void Register<TRegistrar>()
        where TRegistrar : class, IMessagingAssemblyRegistrar, new()
    {
        Registrars.GetOrAdd(typeof(TRegistrar), static _ => new TRegistrar());
    }

    internal static void Apply(IServiceCollection services, Assembly? scopeAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new MessagingAssemblyRegistrationBuilder();
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
        IDictionary<Type, IMessagingAssemblyRegistrar> registrations,
        Assembly? scopeAssembly)
    {
        if (scopeAssembly?.GetCustomAttribute<MessagingAssemblyRegistrarAttribute>() is not { } attribute)
        {
            return;
        }

        if (!typeof(IMessagingAssemblyRegistrar).IsAssignableFrom(attribute.RegistrarType))
        {
            throw new InvalidOperationException(
                $"Assembly '{scopeAssembly.FullName}' declared an invalid LayerZero messaging registrar type '{attribute.RegistrarType.FullName}'.");
        }

        if (Activator.CreateInstance(attribute.RegistrarType) is not IMessagingAssemblyRegistrar registrar)
        {
            throw new InvalidOperationException(
                $"LayerZero messaging registrar '{attribute.RegistrarType.FullName}' could not be created.");
        }

        registrations.TryAdd(attribute.RegistrarType, registrar);
    }
}

internal sealed class EmptyMessageRegistry : IMessageRegistry
{
    public static EmptyMessageRegistry Instance { get; } = new();

    public IReadOnlyList<MessageDescriptor> Messages { get; } = [];

    public bool TryGetDescriptor(Type messageType, out MessageDescriptor descriptor)
    {
        descriptor = null!;
        return false;
    }

    public bool TryGetDescriptor(string messageName, out MessageDescriptor descriptor)
    {
        descriptor = null!;
        return false;
    }
}

internal sealed class CompositeMessageRegistry : IMessageRegistry
{
    private readonly IReadOnlyList<MessageDescriptor> messages;
    private readonly IReadOnlyDictionary<Type, MessageDescriptor> byType;
    private readonly IReadOnlyDictionary<string, MessageDescriptor> byName;

    public CompositeMessageRegistry(IEnumerable<IMessageRegistry> registries)
    {
        ArgumentNullException.ThrowIfNull(registries);

        var byTypeBuilder = new Dictionary<Type, MessageDescriptor>();
        var byNameBuilder = new Dictionary<string, MessageDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in registries
            .SelectMany(static registry => registry.Messages)
            .OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal))
        {
            byNameBuilder.TryAdd(descriptor.Name, descriptor);
            byTypeBuilder.TryAdd(descriptor.MessageType, descriptor);
        }

        messages = byNameBuilder.Values.ToArray();
        byType = byTypeBuilder;
        byName = byNameBuilder;
    }

    public IReadOnlyList<MessageDescriptor> Messages => messages;

    public bool TryGetDescriptor(Type messageType, out MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return byType.TryGetValue(messageType, out descriptor!);
    }

    public bool TryGetDescriptor(string messageName, out MessageDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        return byName.TryGetValue(messageName, out descriptor!);
    }
}

internal sealed class CompositeMessageTopologyManifest : IMessageTopologyManifest
{
    private readonly IReadOnlyList<MessageTopologyDescriptor> messages;
    private readonly IReadOnlyDictionary<Type, MessageTopologyDescriptor> byType;
    private readonly IReadOnlyDictionary<string, MessageTopologyDescriptor> byName;

    public CompositeMessageTopologyManifest(IEnumerable<IMessageTopologyManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        var merged = new Dictionary<string, MessageTopologyDescriptor>(StringComparer.Ordinal);

        foreach (var topology in manifests
            .SelectMany(static manifest => manifest.Messages)
            .OrderBy(static descriptor => descriptor.Message.Name, StringComparer.Ordinal))
        {
            if (merged.TryGetValue(topology.Message.Name, out var existing))
            {
                merged[topology.Message.Name] = new MessageTopologyDescriptor(
                    existing.Message,
                    existing.Subscriptions
                        .Concat(topology.Subscriptions)
                        .GroupBy(static subscription => subscription.Identity, StringComparer.Ordinal)
                        .Select(static group => group.First())
                        .OrderBy(static subscription => subscription.Identity, StringComparer.Ordinal)
                        .ToArray());
                continue;
            }

            merged[topology.Message.Name] = topology;
        }

        messages = merged.Values.ToArray();
        byType = messages.ToDictionary(static descriptor => descriptor.Message.MessageType);
        byName = messages.ToDictionary(static descriptor => descriptor.Message.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<MessageTopologyDescriptor> Messages => messages;

    public bool TryGetDescriptor(Type messageType, out MessageTopologyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return byType.TryGetValue(messageType, out descriptor!);
    }

    public bool TryGetDescriptor(string messageName, out MessageTopologyDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        return byName.TryGetValue(messageName, out descriptor!);
    }
}
