namespace LayerZero.Messaging.Configuration;

/// <summary>
/// Configures LayerZero messaging behavior.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>
    /// Gets message-to-bus routes keyed by logical message name.
    /// </summary>
    public IDictionary<string, string> MessageRoutes { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the logical application name used by consumer-side adapters.
    /// When left unset, <c>AddMessaging()</c> first checks <c>Messaging:ApplicationName</c> and then
    /// falls back to <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ApplicationName" /> when available.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets whether transport topology validators should run at startup.
    /// </summary>
    public bool ValidateTopologyOnStart { get; set; } = true;
}
