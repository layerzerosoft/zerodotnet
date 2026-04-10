using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LayerZero.Messaging.Diagnostics;

internal sealed class MessagingTelemetry
{
    public static readonly MessagingTelemetry Instance = new();

    private MessagingTelemetry()
    {
        ActivitySource = new ActivitySource("LayerZero.Messaging");
        Meter = new Meter("LayerZero.Messaging");
        SentCounter = Meter.CreateCounter<long>("layerzero.messaging.sent");
        PublishedCounter = Meter.CreateCounter<long>("layerzero.messaging.published");
        ProcessedCounter = Meter.CreateCounter<long>("layerzero.messaging.processed");
        FailedCounter = Meter.CreateCounter<long>("layerzero.messaging.failed");
    }

    public ActivitySource ActivitySource { get; }

    public Meter Meter { get; }

    public Counter<long> SentCounter { get; }

    public Counter<long> PublishedCounter { get; }

    public Counter<long> ProcessedCounter { get; }

    public Counter<long> FailedCounter { get; }
}
