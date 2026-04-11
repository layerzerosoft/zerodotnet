using System.ComponentModel.DataAnnotations;

namespace LayerZero.Messaging.Nats.Configuration;

/// <summary>
/// Configures one named NATS JetStream bus.
/// </summary>
public sealed class NatsBusOptions
{
    /// <summary>
    /// Gets or sets the server URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>
    /// Gets or sets whether consumers should run for this bus.
    /// </summary>
    public bool EnableConsumers { get; set; } = true;

    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum delivery count.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxDeliver { get; set; } = 5;
}
