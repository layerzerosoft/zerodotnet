var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var databasePath = Path.Combine(fulfillmentDataDirectory, "fulfillment.db");
var databaseConnectionString = $"Data Source={databasePath}";

var nats = builder.AddNats("nats")
    .WithJetStream();

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
    .WithReference(nats)
    .WithEnvironment("Messaging__Broker", "Nats")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(nats)
    .WithEnvironment("Messaging__Broker", "Nats")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(nats)
    .WithEnvironment("Messaging__Broker", "Nats")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Api>(
        "fulfillment-api",
        static options =>
        {
            options.ExcludeLaunchProfile = true;
            options.ExcludeKestrelEndpoints = true;
        })
    .WithReference(nats)
    .WithEnvironment("Messaging__Broker", "Nats")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WithHttpEndpoint(port: 5384, name: "http", env: "HTTP_PORTS")
    .WithHttpsEndpoint(port: 7384, name: "https", env: "HTTPS_PORTS")
    .WithExternalHttpEndpoints()
    .WaitFor(nats, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.Build().Run();
