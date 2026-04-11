using System.ComponentModel.DataAnnotations;

namespace LayerZero.Messaging.AzureServiceBus.Configuration;

/// <summary>
/// Configures one named Azure Service Bus.
/// </summary>
public sealed class AzureServiceBusBusOptions
{
    /// <summary>
    /// Gets or sets the Service Bus connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional administration connection string. When omitted, <see cref="ConnectionString"/> is used.
    /// </summary>
    public string? AdministrationConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the consumer prefetch count.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum concurrent calls.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxConcurrentCalls { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum auto lock renewal duration.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum delivery count before dead-lettering.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxDeliveryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether consumers should run for this bus.
    /// </summary>
    public bool EnableConsumers { get; set; } = true;
}
