var builder = DistributedApplication.CreateBuilder(args);

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
    var databaseConnectionString = $"Data Source=fulfillment-{profile}.db";

    var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>($"fulfillment-bootstrap-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString);

    var processing = builder.AddProject<Projects.LayerZero_Fulfillment_Processing>($"fulfillment-processing-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(bootstrap);

    var projections = builder.AddProject<Projects.LayerZero_Fulfillment_Projections>($"fulfillment-projections-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WaitFor(bootstrap);

    builder.AddProject<Projects.LayerZero_Fulfillment_Api>($"fulfillment-api-{profile}")
        .WithReference(transport)
        .WithEnvironment("Messaging__Broker", broker)
        .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
        .WithExternalHttpEndpoints()
        .WaitFor(bootstrap)
        .WaitFor(processing)
        .WaitFor(projections);
}
