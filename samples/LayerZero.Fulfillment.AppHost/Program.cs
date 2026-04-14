const int AzureServiceBusAdministrationPort = 5300;
const string AzureServiceBusEmulatorImageTag = "2.0.0";

var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var serviceBus = builder.AddAzureServiceBus("sbemulatorns")
    .RunAsEmulator(static emulator => emulator.WithImageTag(AzureServiceBusEmulatorImageTag))
    .WithEndpoint("emulatorhealth", static endpoint =>
    {
        endpoint.Port = AzureServiceBusAdministrationPort;
    });
var serviceBusAdministrationEndpoint = serviceBus.Resource.GetEndpoint("emulatorhealth");

var kafka = builder.AddKafka("kafka");
RemoveHealthChecks(kafka);

var kafkaReadiness = builder.AddProject<Projects.LayerZero_Fulfillment_KafkaReadiness>("fulfillment-kafka-readiness")
    .WithReference(kafka)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable);

var nats = builder.AddNats("nats")
    .WithJetStream();

AddBrokerProfile("RabbitMq", rabbitMq);
AddBrokerProfile("AzureServiceBus", serviceBus, connectionName: "servicebus", administrationEndpoint: serviceBusAdministrationEndpoint);
AddBrokerProfile("Kafka", kafka, startupDependency: kafkaReadiness);
AddBrokerProfile("Nats", nats);

builder.Build().Run();

void AddBrokerProfile(
    string broker,
    IResourceBuilder<IResourceWithConnectionString> transport,
    string? connectionName = null,
    IResourceBuilder<ProjectResource>? startupDependency = null,
    EndpointReference? administrationEndpoint = null)
{
    var profile = broker.ToLowerInvariant();
    var apiPorts = GetApiPorts(profile);
    var databasePath = Path.Combine(fulfillmentDataDirectory, $"fulfillment-{profile}.db");
    var databaseConnectionString = $"Data Source={databasePath}";

    var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>($"fulfillment-bootstrap-{profile}")
        .WithReference(transport, connectionName)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable);

    if (startupDependency is not null)
    {
        bootstrap = bootstrap.WaitForCompletion(startupDependency);
    }

    var processing = builder.AddProject<Projects.LayerZero_Fulfillment_Processing>($"fulfillment-processing-{profile}")
        .WithReference(transport, connectionName)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitForCompletion(bootstrap);

    var projections = builder.AddProject<Projects.LayerZero_Fulfillment_Projections>($"fulfillment-projections-{profile}")
        .WithReference(transport, connectionName)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitForCompletion(bootstrap);

    if (profile == "azureservicebus")
    {
        if (administrationEndpoint is null)
        {
            throw new InvalidOperationException(
                "Azure Service Bus emulator administration endpoint was not configured for the AppHost profile.");
        }

        bootstrap = WithAzureServiceBusAdministrationConnectionString(bootstrap, transport, administrationEndpoint);
        processing = WithAzureServiceBusAdministrationConnectionString(processing, transport, administrationEndpoint);
        projections = WithAzureServiceBusAdministrationConnectionString(projections, transport, administrationEndpoint);
    }

    builder.AddProject<Projects.LayerZero_Fulfillment_Api>(
            $"fulfillment-api-{profile}",
            static options =>
            {
                options.ExcludeLaunchProfile = true;
                options.ExcludeKestrelEndpoints = true;
            })
        .WithReference(transport, connectionName)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WithHttpEndpoint(port: apiPorts.HttpPort, name: "http", env: "HTTP_PORTS")
        .WithHttpsEndpoint(port: apiPorts.HttpsPort, name: "https", env: "HTTPS_PORTS")
        .WithExternalHttpEndpoints()
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitForCompletion(bootstrap);
}

static (int HttpPort, int HttpsPort) GetApiPorts(string profile)
{
    return profile switch
    {
        "rabbitmq" => (5381, 7381),
        "azureservicebus" => (5382, 7382),
        "kafka" => (5383, 7383),
        "nats" => (5384, 7384),
        _ => throw new InvalidOperationException($"Unsupported broker profile '{profile}'."),
    };
}

static IResourceBuilder<TResource> WithAzureServiceBusAdministrationConnectionString<TResource>(
    IResourceBuilder<TResource> resource,
    IResourceBuilder<IResourceWithConnectionString> transport,
    EndpointReference administrationEndpoint)
    where TResource : IResource, IResourceWithEnvironment
{
    return resource.WithEnvironment(async context =>
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
    });
}

static string BuildAzureServiceBusAdministrationConnectionString(
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

static void RemoveHealthChecks<TResource>(IResourceBuilder<TResource> resource)
    where TResource : IResource
{
    for (var index = resource.Resource.Annotations.Count - 1; index >= 0; index--)
    {
        if (resource.Resource.Annotations[index] is HealthCheckAnnotation)
        {
            resource.Resource.Annotations.RemoveAt(index);
        }
    }
}
