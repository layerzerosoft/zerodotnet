using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.AspNetCore;

/// <summary>
/// Marks one generated LayerZero ASP.NET Core registrar on an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AspNetCoreAssemblyRegistrarAttribute(Type registrarType) : Attribute
{
    /// <summary>
    /// Gets the generated registrar type.
    /// </summary>
    public Type RegistrarType { get; } = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
}

/// <summary>
/// Registers generated LayerZero ASP.NET Core services for one assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAspNetCoreAssemblyRegistrar
{
    /// <summary>
    /// Registers generated LayerZero ASP.NET Core services.
    /// </summary>
    /// <param name="builder">The generated registration builder.</param>
    void Register(AspNetCoreAssemblyRegistrationBuilder builder);
}

/// <summary>
/// Collects generated ASP.NET Core service registrations before they are applied to DI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AspNetCoreAssemblyRegistrationBuilder
{
    private readonly Dictionary<(Type ServiceType, Type ImplementationType, RegistrationKind Kind), ServiceRegistration> registrations = new();

    /// <summary>
    /// Adds one generated service registration.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="kind">The registration kind.</param>
    public void Add<TService, TImplementation>(RegistrationKind kind)
        where TImplementation : class, TService
    {
        registrations.TryAdd(
            (typeof(TService), typeof(TImplementation), kind),
            new ServiceRegistration(typeof(TService), typeof(TImplementation), kind));
    }

    internal void Apply(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var registration in registrations.Values
            .OrderBy(static registration => registration.ServiceType.FullName, StringComparer.Ordinal)
            .ThenBy(static registration => registration.ImplementationType.FullName, StringComparer.Ordinal)
            .ThenBy(static registration => registration.Kind))
        {
            var descriptor = ServiceDescriptor.Scoped(registration.ServiceType, registration.ImplementationType);
            if (registration.Kind == RegistrationKind.TryAddEnumerable)
            {
                services.TryAddEnumerable(descriptor);
            }
            else
            {
                services.TryAdd(descriptor);
            }
        }
    }
}

/// <summary>
/// Collects generated LayerZero ASP.NET Core registrars from loaded assemblies.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspNetCoreAssemblyRegistrarCatalog
{
    private static readonly ConcurrentDictionary<Type, IAspNetCoreAssemblyRegistrar> Registrars = new();

    /// <summary>
    /// Registers one generated registrar instance.
    /// </summary>
    /// <param name="registrar">The generated registrar.</param>
    public static void Register(IAspNetCoreAssemblyRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Registrars.TryAdd(registrar.GetType(), registrar);
    }

    /// <summary>
    /// Registers one generated registrar type.
    /// </summary>
    /// <typeparam name="TRegistrar">The generated registrar type.</typeparam>
    public static void Register<TRegistrar>()
        where TRegistrar : class, IAspNetCoreAssemblyRegistrar, new()
    {
        Registrars.GetOrAdd(typeof(TRegistrar), static _ => new TRegistrar());
    }

    internal static void Apply(IServiceCollection services, Assembly? scopeAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new AspNetCoreAssemblyRegistrationBuilder();
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

/// <summary>
/// Identifies how one generated ASP.NET Core service registration should be applied.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum RegistrationKind
{
    /// <summary>
    /// Applies the registration with <c>TryAdd</c>.
    /// </summary>
    TryAdd = 0,

    /// <summary>
    /// Applies the registration with <c>TryAddEnumerable</c>.
    /// </summary>
    TryAddEnumerable = 1,
}

internal sealed record ServiceRegistration(Type ServiceType, Type ImplementationType, RegistrationKind Kind);
