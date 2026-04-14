namespace LayerZero.Fulfillment.EndToEnd.Tests;

[Trait("Category", "LocalFast")]
public sealed class RabbitMqFulfillmentEndToEndTests(RabbitMqFulfillmentHarnessFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<RabbitMqFulfillmentHarnessFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected override FulfillmentHarness Harness => fixture.Harness;

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        var response = await Harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

[Trait("Category", "MatrixOnly")]
public sealed class AzureServiceBusFulfillmentEndToEndTests(AzureServiceBusFulfillmentFixture fixture) : PerTestFulfillmentEndToEndTestBase, IClassFixture<AzureServiceBusFulfillmentFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        var response = await Harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

[Trait("Category", "CloudOptional")]
public sealed class CloudAzureServiceBusFulfillmentEndToEndTests(CloudAzureServiceBusFulfillmentFixture fixture) : PerTestFulfillmentEndToEndTestBase, IClassFixture<CloudAzureServiceBusFulfillmentFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING"));

    protected override Task<FulfillmentHarness> CreateHarnessAsync()
    {
        return FulfillmentHarness.CreateAsync(fixture, TestContext.Current.CancellationToken);
    }
}

[Trait("Category", "MatrixOnly")]
public sealed class KafkaFulfillmentEndToEndTests(KafkaFulfillmentHarnessFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<KafkaFulfillmentHarnessFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected override FulfillmentHarness Harness => fixture.Harness;

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        var response = await Harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

[Trait("Category", "MatrixOnly")]
public sealed class NatsFulfillmentEndToEndTests(NatsFulfillmentHarnessFixture fixture) : FulfillmentEndToEndTestBase, IClassFixture<NatsFulfillmentHarnessFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected override FulfillmentHarness Harness => fixture.Harness;

    [Fact]
    public async Task Startup_completes_and_openapi_is_reachable()
    {
        var response = await Harness.Client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
