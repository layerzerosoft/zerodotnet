namespace LayerZero.Fulfillment.EndToEnd.Tests;

public abstract class FulfillmentHarnessFixture<TBrokerFixture> : IAsyncLifetime
    where TBrokerFixture : IFulfillmentBrokerFixture, IAsyncLifetime, new()
{
    private FulfillmentHarness? harness;

    protected TBrokerFixture BrokerFixture { get; } = new();

    public FulfillmentHarness Harness => harness
        ?? throw new InvalidOperationException("The fulfillment harness has not been initialized.");

    public virtual async ValueTask InitializeAsync()
    {
        await BrokerFixture.InitializeAsync().ConfigureAwait(false);
        harness = await FulfillmentHarness.CreateAsync(BrokerFixture).ConfigureAwait(false);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (harness is not null)
        {
            await harness.DisposeAsync().ConfigureAwait(false);
            harness = null;
        }

        await BrokerFixture.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class RabbitMqFulfillmentHarnessFixture : FulfillmentHarnessFixture<RabbitMqFulfillmentFixture>;

public sealed class KafkaFulfillmentHarnessFixture : FulfillmentHarnessFixture<KafkaFulfillmentFixture>;

public sealed class NatsFulfillmentHarnessFixture : FulfillmentHarnessFixture<NatsFulfillmentFixture>;
