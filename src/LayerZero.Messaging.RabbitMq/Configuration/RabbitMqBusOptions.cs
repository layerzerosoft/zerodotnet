using System.ComponentModel.DataAnnotations;

namespace LayerZero.Messaging.RabbitMq.Configuration;

/// <summary>
/// Configures one named RabbitMQ bus.
/// </summary>
public sealed class RabbitMqBusOptions
{
    /// <summary>
    /// Gets or sets the AMQP connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consumer prefetch count.
    /// </summary>
    [Range(1, ushort.MaxValue)]
    public ushort PrefetchCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum delivery attempts before dead-lettering.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the publisher confirm timeout.
    /// </summary>
    public TimeSpan PublisherConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets whether consumers should run for this bus.
    /// </summary>
    public bool EnableConsumers { get; set; } = true;
}
