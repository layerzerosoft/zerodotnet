using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Client;

/// <summary>
/// Registers explicit LayerZero HTTP clients with <see cref="IHttpClientFactory"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed LayerZero HTTP client.
    /// </summary>
    /// <typeparam name="TClient">The typed client type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Optional HTTP client configuration.</param>
    /// <returns>The HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddLayerZeroClient<TClient>(
        this IServiceCollection services,
        Action<HttpClient>? configureClient = null)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);

        IHttpClientBuilder builder = services.AddHttpClient<TClient>();
        if (configureClient is not null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        return builder;
    }
}
