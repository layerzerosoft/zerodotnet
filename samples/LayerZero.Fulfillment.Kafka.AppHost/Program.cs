var builder = DistributedApplication.CreateBuilder(args);
var kafka = builder.AddKafka("kafka");
var postgres = builder.AddPostgres("postgres");
var fulfillmentDatabase = postgres.AddDatabase("fulfillment");
RemoveHealthChecks(kafka);

var kafkaReadiness = builder.AddProject<Projects.LayerZero_Fulfillment_KafkaReadiness>("fulfillment-kafka-readiness")
    .WithReference(kafka)
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable);

var bootstrap = builder.AddProject<Projects.LayerZero_Fulfillment_Kafka_Bootstrap>("fulfillment-bootstrap")
    .WithReference(kafka)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(kafkaReadiness);

builder.AddProject<Projects.LayerZero_Fulfillment_Kafka_Processing>("fulfillment-processing")
    .WithReference(kafka)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Kafka_Projections>("fulfillment-projections")
    .WithReference(kafka)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
    .WaitForCompletion(bootstrap);

builder.AddProject<Projects.LayerZero_Fulfillment_Kafka_Api>("fulfillment-api", launchProfileName: null)
    .WithReference(kafka)
    .WithReference(fulfillmentDatabase, connectionName: "Fulfillment")
    .WithHttpEndpoint(port: 5383, name: "http")
    .WithHttpsEndpoint(port: 7383, name: "https")
    .WithUrlForEndpoint("http", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTP)" })
    .WithUrlForEndpoint("https", static _ => new() { Url = "/openapi/v1.json", DisplayText = "OpenAPI (HTTPS)" })
    .WaitFor(kafka, WaitBehavior.StopOnResourceUnavailable)
    .WaitFor(fulfillmentDatabase, WaitBehavior.StopOnResourceUnavailable)
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
