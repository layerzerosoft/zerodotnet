using LayerZero.AspNetCore;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Api;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LayerZero.Fulfillment.AzureServiceBus.Api;

public static class AzureServiceBusFulfillmentApiHost
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOpenApi("v1", options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });
        services.AddHealthChecks();
        services.AddData().UsePostgres("Fulfillment");
        services.AddFulfillmentStore();
        services.AddMessaging(ResolveApplicationName(configuration))
            .AddAzureServiceBus(configuration, role: MessageTransportRole.SendOnly);
        services.AddLayerZero().AddSlices();
    }

    public static void ConfigureApplication(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
        }

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
        app.MapSlices();
    }

    private static string ResolveApplicationName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rootName = configuration["Messaging:ApplicationName"];
        return string.IsNullOrWhiteSpace(rootName)
            ? "fulfillment-azure-service-bus"
            : rootName;
    }
}

public sealed class AzureServiceBusFulfillmentApiEntryPoint;
