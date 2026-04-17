var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var databasePath = Path.Combine(fulfillmentDataDirectory, "fulfillment.db");
var databaseConnectionString = $"Data Source={databasePath}";

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
    .WithReference(rabbitMq)
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(rabbitMq)
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(rabbitMq)
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Api>(
        "fulfillment-api",
        static options =>
        {
            options.ExcludeLaunchProfile = true;
            options.ExcludeKestrelEndpoints = true;
        })
    .WithReference(rabbitMq)
    .WithEnvironment("Messaging__Broker", "RabbitMq")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WithHttpEndpoint(port: 5381, name: "http", env: "HTTP_PORTS")
    .WithHttpsEndpoint(port: 7381, name: "https", env: "HTTPS_PORTS")
    .WithExternalHttpEndpoints()
    .WaitFor(rabbitMq, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.Build().Run();
