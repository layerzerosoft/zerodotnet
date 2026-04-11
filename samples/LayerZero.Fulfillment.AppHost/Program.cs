using System.IO;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var kafka = builder.AddKafka("kafka");
var nats = builder.AddNats("nats")
    .WithJetStream();

AddBrokerProfile("RabbitMq", rabbitMq);
AddBrokerProfile("AzureServiceBus", serviceBus);
AddBrokerProfile("Kafka", kafka);
AddBrokerProfile("Nats", nats);

builder.Build().Run();

void AddBrokerProfile(string broker, IResourceBuilder<IResourceWithConnectionString> transport)
{
    var profile = broker.ToLowerInvariant();
    var apiPorts = GetApiPorts(profile);
    var databasePath = Path.Combine(fulfillmentDataDirectory, $"fulfillment-{profile}.db");
    var databaseConnectionString = $"Data Source={databasePath}";

    var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>($"fulfillment-bootstrap-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable);

    var processing = builder.AddProject<Projects.LayerZero_Fulfillment_Processing>($"fulfillment-processing-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitFor(bootstrap, WaitBehavior.StopOnResourceUnavailable);

    var projections = builder.AddProject<Projects.LayerZero_Fulfillment_Projections>($"fulfillment-projections-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitFor(bootstrap, WaitBehavior.StopOnResourceUnavailable);

    builder.AddProject<Projects.LayerZero_Fulfillment_Api>(
            $"fulfillment-api-{profile}",
            static options =>
            {
                options.ExcludeLaunchProfile = true;
                options.ExcludeKestrelEndpoints = true;
            })
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WithHttpEndpoint(port: apiPorts.HttpPort, name: "http", env: "HTTP_PORTS")
        .WithHttpsEndpoint(port: apiPorts.HttpsPort, name: "https", env: "HTTPS_PORTS")
        .WithExternalHttpEndpoints()
        .WaitFor(transport, WaitBehavior.StopOnResourceUnavailable)
        .WaitFor(bootstrap, WaitBehavior.StopOnResourceUnavailable)
        .WaitFor(processing, WaitBehavior.StopOnResourceUnavailable)
        .WaitFor(projections, WaitBehavior.StopOnResourceUnavailable);
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
