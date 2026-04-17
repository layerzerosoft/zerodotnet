var builder = DistributedApplication.CreateBuilder(args);
var fulfillmentDataDirectory = Path.Combine(builder.AppHostDirectory, "data");
Directory.CreateDirectory(fulfillmentDataDirectory);

var databasePath = Path.Combine(fulfillmentDataDirectory, "fulfillment.db");
var databaseConnectionString = $"Data Source={databasePath}";

var kafka = builder.AddKafka("kafka");
RemoveHealthChecks(kafka);

var kafkaReadiness = builder.AddProject<Projects.LayerZero_Fulfillment_KafkaReadiness>("fulfillment-kafka-readiness")
    .WithReference(kafka)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable);

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Bootstrap>("fulfillment-bootstrap")
    .WithReference(kafka)
    .WithEnvironment("Messaging__Broker", "Kafka")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(kafkaReadiness);

builder.AddProject<Projects.LayerZero_Fulfillment_Processing>("fulfillment-processing")
    .WithReference(kafka)
    .WithEnvironment("Messaging__Broker", "Kafka")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Projections>("fulfillment-projections")
    .WithReference(kafka)
    .WithEnvironment("Messaging__Broker", "Kafka")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Api>("fulfillment-api", launchProfileName: null)
    .WithReference(kafka)
    .WithEnvironment("Messaging__Broker", "Kafka")
    .WithEnvironment("ConnectionStrings__Fulfillment", databaseConnectionString)
    .WithHttpEndpoint(port: 5383, name: "http")
    .WithHttpsEndpoint(port: 7383, name: "https")
    .WithUrlForEndpoint("http", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTP)" })
    .WithUrlForEndpoint("https", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTPS)" })
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.Build().Run();

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
