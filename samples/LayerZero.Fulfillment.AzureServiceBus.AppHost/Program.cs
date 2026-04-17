const string AzureServiceBusEmulatorImageTag = "2.0.0";
const int AzureServiceBusAdministrationContainerPort = 5300;

var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var databasePath = Path.Combine(fulfillmentDataDirectory, "fulfillment.db");
var databaseConnectionString = $"Data Source={databasePath}";

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(static emulator => emulator.WithImageTag(AzureServiceBusEmulatorImageTag))
    .WithEndpoint("administration", static endpoint =>
    {
        endpoint.TargetPort = AzureServiceBusAdministrationContainerPort;
        endpoint.UriScheme = "sb";
    });
var administrationEndpoint = serviceBus.Resource.GetEndpoint("administration");

var bootstrap = WithAzureServiceBusAdministrationConnectionString(
    builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
        .WithReference(serviceBus)
        .WithEnvironment("Messaging__Broker", "AzureServiceBus")
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(serviceBus, WaitBehavior.StopOnResourceUnavailable),
    serviceBus,
    administrationEndpoint);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(serviceBus)
    .WithEnvironment("Messaging__Broker", "AzureServiceBus")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(serviceBus, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap)
    .WithEnvironment(context => ConfigureAzureServiceBusAdministrationConnectionString(
        context,
        serviceBus,
        administrationEndpoint));

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(serviceBus)
    .WithEnvironment("Messaging__Broker", "AzureServiceBus")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(serviceBus, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap)
    .WithEnvironment(context => ConfigureAzureServiceBusAdministrationConnectionString(
        context,
        serviceBus,
        administrationEndpoint));

builder.AddProject<Projects.LayerZero_Fulfillment_Api>("fulfillment-api", launchProfileName: null)
    .WithReference(serviceBus)
    .WithEnvironment("Messaging__Broker", "AzureServiceBus")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WithHttpEndpoint(port: 5382, name: "http")
    .WithHttpsEndpoint(port: 7382, name: "https")
    .WithUrlForEndpoint("http", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTP)" })
    .WithUrlForEndpoint("https", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTPS)" })
    .WaitFor(serviceBus, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap)
    .WithEnvironment(context => ConfigureAzureServiceBusAdministrationConnectionString(
        context,
        serviceBus,
        administrationEndpoint));

builder.Build().Run();

IResourceBuilder<TResource> WithAzureServiceBusAdministrationConnectionString<TResource>(
    IResourceBuilder<TResource> resource,
    IResourceBuilder<IResourceWithConnectionString> transport,
    EndpointReference administrationEndpoint)
    where TResource : IResource, IResourceWithEnvironment
{
    return resource.WithEnvironment(context => ConfigureAzureServiceBusAdministrationConnectionString(
        context,
        transport,
        administrationEndpoint));
}

async Task ConfigureAzureServiceBusAdministrationConnectionString(
    EnvironmentCallbackContext context,
    IResourceBuilder<IResourceWithConnectionString> transport,
    EndpointReference administrationEndpoint)
{
    var connectionString = await transport.Resource.GetConnectionStringAsync(context.CancellationToken).ConfigureAwait(false);
    var administrationEndpointUrl = await administrationEndpoint.GetValueAsync(context.CancellationToken).ConfigureAwait(false);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Resource '{transport.Resource.Name}' did not provide a connection string for Azure Service Bus administration.");
    }

    if (string.IsNullOrWhiteSpace(administrationEndpointUrl))
    {
        throw new InvalidOperationException(
            "Azure Service Bus emulator administration endpoint did not resolve to a usable URL.");
    }

    context.EnvironmentVariables["Messaging__AzureServiceBus__AdministrationConnectionString"] =
        BuildAzureServiceBusAdministrationConnectionString(connectionString, administrationEndpointUrl);
}

string BuildAzureServiceBusAdministrationConnectionString(
    string connectionString,
    string administrationEndpointUrl)
{
    if (!Uri.TryCreate(administrationEndpointUrl, UriKind.Absolute, out var administrationUri))
    {
        throw new InvalidOperationException(
            $"Azure Service Bus administration endpoint '{administrationEndpointUrl}' is not a valid absolute URI.");
    }

    var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var rewrittenSegments = new List<string>(segments.Length + 1)
    {
        $"Endpoint={new UriBuilder("sb", administrationUri.Host, administrationUri.Port)}",
    };
    var hasSharedAccessKeyName = false;
    var hasSharedAccessKey = false;
    var hasUseDevelopmentEmulator = false;

    foreach (var segment in segments)
    {
        var separatorIndex = segment.IndexOf('=');
        if (separatorIndex <= 0)
        {
            rewrittenSegments.Add(segment);
            continue;
        }

        var key = segment[..separatorIndex];
        if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (key.Equals("SharedAccessKeyName", StringComparison.OrdinalIgnoreCase))
        {
            hasSharedAccessKeyName = true;
        }
        else if (key.Equals("SharedAccessKey", StringComparison.OrdinalIgnoreCase))
        {
            hasSharedAccessKey = true;
        }
        else if (key.Equals("UseDevelopmentEmulator", StringComparison.OrdinalIgnoreCase))
        {
            hasUseDevelopmentEmulator = true;
        }

        rewrittenSegments.Add(segment);
    }

    if (!hasSharedAccessKeyName || !hasSharedAccessKey)
    {
        throw new InvalidOperationException(
            "Azure Service Bus emulator connection string did not include SharedAccessKeyName and SharedAccessKey segments.");
    }

    if (!hasUseDevelopmentEmulator)
    {
        rewrittenSegments.Add("UseDevelopmentEmulator=true");
    }

    return string.Join(';', rewrittenSegments) + ';';
}
