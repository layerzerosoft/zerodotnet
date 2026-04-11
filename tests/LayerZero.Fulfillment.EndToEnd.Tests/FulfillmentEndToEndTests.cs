namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class RabbitMqFulfillmentEndToEndTests(RabbitMqFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<RabbitMqFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        await using var harness = await CreateHarnessAsync();
        var response = await harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class AzureServiceBusFulfillmentEndToEndTests(AzureServiceBusFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<AzureServiceBusFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        await using var harness = await CreateHarnessAsync();
        var response = await harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
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

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        await using var harness = await CreateHarnessAsync();
        var response = await harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class NatsFulfillmentEndToEndTests(NatsFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<NatsFulfillmentFixture>
{
    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        await using var harness = await CreateHarnessAsync();
        var response = await harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
