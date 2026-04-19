var builder = DistributedApplication.CreateBuilder(args);
var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();
var postgres = builder.AddPostgres("postgres");
var fulfillmentDatabase = postgres.AddDatabase("fulfillment");

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
    .WithReference(rabbitMq)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(rabbitMq)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(rabbitMq)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Api>("fulfillment-api", launchProfileName: null)
    .WithReference(rabbitMq)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WithHttpEndpoint(port: 5381, name: "http")
    .WithHttpsEndpoint(port: 7381, name: "https")
    .WithUrlForEndpoint("http", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTP)" })
    .WithUrlForEndpoint("https", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTPS)" })
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.Build().Run();
