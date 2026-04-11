using System.Text.Json;

namespace LayerZero.Testcontainers.Cleanup;

internal static class DockerInspectParser
{
    public static IReadOnlyList<DockerResourceRecord> Parse(string json, DockerResourceKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("docker inspect output must be a JSON array.");
        }

        var resources = new List<DockerResourceRecord>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            resources.Add(kind switch
            {
                DockerResourceKind.Container => ParseContainer(element),
                DockerResourceKind.Network => ParseNetwork(element),
                DockerResourceKind.Volume => ParseVolume(element),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Docker resource kind."),
            });
        }

        return resources;
    }

    private static DockerResourceRecord ParseContainer(JsonElement element)
    {
        var id = element.GetProperty("Id").GetString() ?? throw new InvalidOperationException("Container inspect result is missing Id.");
        var name = (element.GetProperty("Name").GetString() ?? string.Empty).TrimStart('/');
        var createdAt = ParseCreatedAt(element, "Created");
        var labels = ParseLabels(element.GetProperty("Config"), "Labels");

        return new DockerResourceRecord(id, name, DockerResourceKind.Container, createdAt, labels);
    }

    private static DockerResourceRecord ParseNetwork(JsonElement element)
    {
        var id = element.GetProperty("Id").GetString() ?? throw new InvalidOperationException("Network inspect result is missing Id.");
        var name = element.GetProperty("Name").GetString() ?? id;
        var createdAt = ParseCreatedAt(element, "Created");
        var labels = ParseLabels(element, "Labels");

        return new DockerResourceRecord(id, name, DockerResourceKind.Network, createdAt, labels);
    }

    private static DockerResourceRecord ParseVolume(JsonElement element)
    {
        var name = element.GetProperty("Name").GetString() ?? throw new InvalidOperationException("Volume inspect result is missing Name.");
        var createdAt = ParseCreatedAt(element, "CreatedAt");
        var labels = ParseLabels(element, "Labels");

        return new DockerResourceRecord(name, name, DockerResourceKind.Volume, createdAt, labels);
    }

    private static DateTimeOffset? ParseCreatedAt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return DateTimeOffset.TryParse(value, out var createdAt)
            ? createdAt
            : null;
    }

    private static IReadOnlyDictionary<string, string> ParseLabels(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return property
            .EnumerateObject()
            .Where(static label => label.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                static label => label.Name,
                static label => label.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }
}
