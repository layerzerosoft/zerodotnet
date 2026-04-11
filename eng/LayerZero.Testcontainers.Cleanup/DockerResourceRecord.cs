namespace LayerZero.Testcontainers.Cleanup;

internal sealed record DockerResourceRecord(
    string Id,
    string Name,
    DockerResourceKind Kind,
    DateTimeOffset? CreatedAtUtc,
    IReadOnlyDictionary<string, string> Labels)
{
    public string? SessionId =>
        Labels.TryGetValue(CleanupLabels.SessionIdLabel, out var sessionId)
            ? sessionId
            : null;
}
