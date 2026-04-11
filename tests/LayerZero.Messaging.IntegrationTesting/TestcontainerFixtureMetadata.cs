namespace LayerZero.Messaging.IntegrationTesting;

public sealed class TestcontainerFixtureMetadata
{
    public const string RepositoryLabel = "layerzero.test.repo";
    public const string ProjectLabel = "layerzero.test.project";
    public const string BrokerLabel = "layerzero.test.broker";
    public const string RunIdLabel = "layerzero.test.run-id";
    public const string RepositoryName = "zero" + "dotnet";

    public TestcontainerFixtureMetadata(string projectName, string brokerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerName);

        ProjectName = projectName;
        BrokerName = brokerName;
        RunId = Guid.NewGuid().ToString("N");
        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RepositoryLabel] = RepositoryName,
            [ProjectLabel] = projectName,
            [BrokerLabel] = brokerName,
            [RunIdLabel] = RunId,
        };
    }

    public string ProjectName { get; }

    public string BrokerName { get; }

    public string RunId { get; }

    public IReadOnlyDictionary<string, string> Labels { get; }
}
