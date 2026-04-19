var builder = DistributedApplication.CreateBuilder(args);
var nats = builder.AddNats("nats")
    .WithJetStream();
var postgres = builder.AddPostgres("postgres");
var fulfillmentDatabase = postgres.AddDatabase("fulfillment");

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
    .WithReference(nats)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "Nats")
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(nats)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "Nats")
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(nats)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "Nats")
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Api>("fulfillment-api", launchProfileName: null)
    .WithReference(nats)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithEnvironment("Messaging__Broker", "Nats")
    .WithHttpEndpoint(port: 5384, name: "http")
    .WithHttpsEndpoint(port: 7384, name: "https")
    .WithUrlForEndpoint("http", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTP)" })
    .WithUrlForEndpoint("https", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTPS)" })
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.Build().Run();
