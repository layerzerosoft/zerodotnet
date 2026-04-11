using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using LayerZero.Messaging.Configuration;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaTopologyManager(
    string name,
    KafkaClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions) : IMessageTopologyManager
{
    private readonly string busName = name;
    private readonly KafkaClientProvider clientProvider = clientProvider;
    private readonly IMessageTopologyManifest topologyManifest = topologyManifest;
    private readonly IMessageRouteResolver routeResolver = routeResolver;
    private readonly IMessageConventions conventions = conventions;
    private readonly IOptions<MessagingOptions> messagingOptions = messagingOptions;

    public string Name => busName;

    public ValueTask ValidateAsync(CancellationToken cancellationToken = default)
    {
        var metadata = clientProvider.GetAdminClient().GetMetadata(TimeSpan.FromSeconds(10));
        var topics = metadata.Topics.Select(static topic => topic.Topic).ToHashSet(StringComparer.Ordinal);

        foreach (var topology in GetTopologies())
        {
            var topicName = conventions.GetEntityName(topology.Message);
            EnsureTopicExists(topics, topicName);

            foreach (var retryAndDeadLetter in GetRetryAndDeadLetterTopics(topology))
            {
                EnsureTopicExists(topics, retryAndDeadLetter.RetryTopicName);
                EnsureTopicExists(topics, retryAndDeadLetter.DeadLetterTopicName);
            }
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var expectedTopics = GetExpectedTopics().ToArray();
        var metadata = clientProvider.GetAdminClient().GetMetadata(TimeSpan.FromSeconds(10));
        var topics = metadata.Topics.Select(static topic => topic.Topic).ToHashSet(StringComparer.Ordinal);
        var topicsToCreate = new List<TopicSpecification>();

        foreach (var topicName in expectedTopics)
        {
            AddTopic(topics, topicsToCreate, topicName);
        }

        if (topicsToCreate.Count > 0)
        {
            await clientProvider.GetAdminClient()
                .CreateTopicsAsync(topicsToCreate)
                .ConfigureAwait(false);
        }

        await WaitForTopicsAsync(expectedTopics, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<MessageTopologyDescriptor> GetTopologies()
    {
        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), busName, StringComparison.Ordinal));
    }

    private IEnumerable<string> GetExpectedTopics()
    {
        foreach (var topology in GetTopologies())
        {
            yield return conventions.GetEntityName(topology.Message);

            foreach (var retryAndDeadLetter in GetRetryAndDeadLetterTopics(topology))
            {
                yield return retryAndDeadLetter.RetryTopicName;
                yield return retryAndDeadLetter.DeadLetterTopicName;
            }
        }
    }

    private IEnumerable<(string RetryTopicName, string DeadLetterTopicName)> GetRetryAndDeadLetterTopics(MessageTopologyDescriptor topology)
    {
        var applicationName = messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"Kafka bus '{busName}' requires MessagingOptions.ApplicationName when consumers are enabled.");

        return topology.Subscriptions
            .Select(subscription => subscription.GetSubscriptionName(applicationName))
            .Distinct(StringComparer.Ordinal)
            .Select(subscriptionName => (
                RetryTopicName: MessageTopologyNames.Retry(subscriptionName, "default"),
                DeadLetterTopicName: MessageTopologyNames.DeadLetter(subscriptionName)));
    }

    private void AddTopic(ISet<string> existingTopics, ICollection<TopicSpecification> topicsToCreate, string topicName)
    {
        if (existingTopics.Contains(topicName))
        {
            return;
        }

        topicsToCreate.Add(new TopicSpecification
        {
            Name = topicName,
            NumPartitions = clientProvider.Options.PartitionCount,
            ReplicationFactor = clientProvider.Options.ReplicationFactor,
        });
        existingTopics.Add(topicName);
    }

    private static void EnsureTopicExists(ISet<string> topics, string topicName)
    {
        if (!topics.Contains(topicName))
        {
            throw new InvalidOperationException($"Kafka topic '{topicName}' does not exist.");
        }
    }

    private async Task WaitForTopicsAsync(IReadOnlyCollection<string> expectedTopics, CancellationToken cancellationToken)
    {
        if (expectedTopics.Count == 0)
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = clientProvider.GetAdminClient().GetMetadata(TimeSpan.FromSeconds(10));
            var topics = metadata.Topics.Select(static topic => topic.Topic).ToHashSet(StringComparer.Ordinal);
            if (expectedTopics.All(topics.Contains))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        var finalMetadata = clientProvider.GetAdminClient().GetMetadata(TimeSpan.FromSeconds(10));
        var finalTopics = finalMetadata.Topics.Select(static topic => topic.Topic).ToHashSet(StringComparer.Ordinal);
        var missingTopics = expectedTopics.Where(topic => !finalTopics.Contains(topic)).ToArray();
        throw new InvalidOperationException($"Kafka topics were not provisioned in time: {string.Join(", ", missingTopics)}.");
    }
}
