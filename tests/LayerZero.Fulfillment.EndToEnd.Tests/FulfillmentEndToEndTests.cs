namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class RabbitMqFulfillmentEndToEndTests(RabbitMqFulfillmentFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<RabbitMqFulfillmentFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

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
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

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
    public new static bool SkipWhenCloudEnvironmentUnavailable =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING"));

    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}

public sealed class KafkaFulfillmentEndToEndTests : FulfillmentEndToEndTestBase, IAsyncLifetime
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

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
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

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
