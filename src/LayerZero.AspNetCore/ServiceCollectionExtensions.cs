using LayerZero.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LayerZero.AspNetCore;

/// <summary>
/// Registers LayerZero services for ASP.NET Core applications.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the baseline LayerZero services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddLayerZero(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var scopeAssembly = Assembly.GetCallingAssembly();
        services.AddProblemDetails();
        services.AddValidation();
        AspNetCoreAssemblyRegistrarCatalog.Apply(services, scopeAssembly);
        return services;
    }

    /// <summary>
    /// Registers a validator for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddValidator<TRequest, TValidator>(this IServiceCollection services)
        where TValidator : class, IValidator<TRequest>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator<TRequest>, TValidator>());
        return services;
    }
}
