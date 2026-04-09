using LayerZero.ZeroDotNet.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.ZeroDotNet.AspNetCore;

/// <summary>
/// Registers ZeroDotNet services for ASP.NET Core applications.
/// </summary>
public static class ZeroServiceCollectionExtensions
{
    /// <summary>
    /// Adds the baseline ZeroDotNet services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddZeroDotNet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    /// <summary>
    /// Registers a vertical-slice handler as a scoped service.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddZeroSlice<THandler>(this IServiceCollection services)
        where THandler : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAdd(ServiceDescriptor.Scoped<THandler, THandler>());
        return services;
    }

    /// <summary>
    /// Registers a validator for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddZeroValidator<TRequest, TValidator>(this IServiceCollection services)
        where TValidator : class, IZeroValidator<TRequest>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IZeroValidator<TRequest>, TValidator>());
        return services;
    }
}
