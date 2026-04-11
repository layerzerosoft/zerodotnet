using LayerZero.Testcontainers.Cleanup;

namespace LayerZero.Testcontainers.Cleanup.Tests;

public sealed class DockerInspectParserTests
{
    [Fact]
    public void Parse_reads_container_labels_from_docker_inspect_json()
    {
        var repositoryName = "zero" + "dotnet";
        var json = $$"""
            [
              {
                "Id": "container-1",
                "Name": "/rabbitmq-1",
                "Created": "2026-04-11T10:00:00Z",
                "Config": {
                  "Labels": {
                    "layerzero.test.repo": "{{repositoryName}}",
                    "org.testcontainers.session-id": "session-1"
                  }
                }
              }
            ]
            """;

        var resources = DockerInspectParser.Parse(json, DockerResourceKind.Container);

        var resource = Assert.Single(resources);
        Assert.Equal("container-1", resource.Id);
        Assert.Equal("rabbitmq-1", resource.Name);
        Assert.Equal("session-1", resource.SessionId);
        Assert.Equal(CleanupLabels.RepositoryName, resource.Labels[CleanupLabels.RepositoryLabel]);
    }

    [Fact]
    public void Parse_reads_network_and_volume_shapes()
    {
        const string networkJson = """
            [
              {
                "Id": "network-1",
                "Name": "bridge-network",
                "Created": "2026-04-11T10:00:00Z",
                "Labels": {
                  "org.testcontainers.session-id": "session-1"
                }
              }
            ]
            """;
        const string volumeJson = """
            [
              {
                "Name": "volume-1",
                "CreatedAt": "2026-04-11T10:00:00Z",
                "Labels": {
                  "org.testcontainers.session-id": "session-1"
                }
              }
            ]
            """;

        var network = Assert.Single(DockerInspectParser.Parse(networkJson, DockerResourceKind.Network));
        var volume = Assert.Single(DockerInspectParser.Parse(volumeJson, DockerResourceKind.Volume));

        Assert.Equal("bridge-network", network.Name);
        Assert.Equal("volume-1", volume.Id);
        Assert.Equal("session-1", network.SessionId);
        Assert.Equal("session-1", volume.SessionId);
    }
}
