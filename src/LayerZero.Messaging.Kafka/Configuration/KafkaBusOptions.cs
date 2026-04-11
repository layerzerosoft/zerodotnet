using System.ComponentModel.DataAnnotations;

namespace LayerZero.Messaging.Kafka.Configuration;

/// <summary>
/// Configures one named Kafka bus.
/// </summary>
public sealed class KafkaBusOptions
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers.
    /// </summary>
    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether consumers should run for this bus.
    /// </summary>
    public bool EnableConsumers { get; set; } = true;

    /// <summary>
    /// Gets or sets the consumer poll interval.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets the maximum delivery attempts before dead-lettering.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the topic replication factor used during provisioning.
    /// </summary>
    [Range(1, short.MaxValue)]
    public short ReplicationFactor { get; set; } = 1;

    /// <summary>
    /// Gets or sets the topic partition count used during provisioning.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int PartitionCount { get; set; } = 1;
}
