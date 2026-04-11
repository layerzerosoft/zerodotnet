namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class RabbitMqFulfillmentEndToEndTests(RabbitMqFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<RabbitMqFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}

public sealed class AzureServiceBusFulfillmentEndToEndTests(AzureServiceBusFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<AzureServiceBusFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}

public sealed class CloudAzureServiceBusFulfillmentEndToEndTests(CloudAzureServiceBusFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<CloudAzureServiceBusFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}

public sealed class KafkaFulfillmentEndToEndTests : FulfillmentEndToEndTestBase, IAsyncLifetime
{
    private readonly KafkaFulfillmentFixture fixture = new();

    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        await fixture.InitializeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed class NatsFulfillmentEndToEndTests(NatsFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<NatsFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}
