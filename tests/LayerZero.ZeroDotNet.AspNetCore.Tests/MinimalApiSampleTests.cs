using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LayerZero.ZeroDotNet.AspNetCore.Tests;

public sealed class MinimalApiSampleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public MinimalApiSampleTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Pulse_endpoint_maps_sync_vertical_slice()
    {
        HttpClient client = factory.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        JsonObject? response = await client.GetFromJsonAsync<JsonObject>("/pulse", cancellationToken);

        Assert.Equal("ready", response?["status"]?.GetValue<string>());
        Assert.Equal("net10.0", response?["baseline"]?.GetValue<string>());
    }

    [Fact]
    public async Task Widgets_endpoint_maps_async_slice_and_trims_name()
    {
        HttpClient client = factory.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        HttpResponseMessage response = await client.PostAsJsonAsync("/widgets", new { name = "  Nova  " }, cancellationToken);

        response.EnsureSuccessStatusCode();
        JsonObject? body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        Assert.Equal("Nova", body?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task Widgets_endpoint_returns_problem_details_for_validation_failure()
    {
        HttpClient client = factory.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        HttpResponseMessage response = await client.PostAsJsonAsync("/widgets", new { name = "" }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonObject? body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        Assert.Equal("Validation failed.", body?["title"]?.GetValue<string>());
        Assert.NotNull(body?["errors"]?["Name"]);
        Assert.NotNull(body?["zero.errors"]);
    }

    [Fact]
    public async Task OpenApi_document_is_available_without_swashbuckle_or_nswag()
    {
        HttpClient client = factory.CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string document = await client.GetStringAsync("/openapi/v1.json", cancellationToken);
        JsonObject openApi = JsonNode.Parse(document)!.AsObject();

        string? openApiVersion = openApi["openapi"]?.GetValue<string>();
        Assert.NotNull(openApiVersion);
        Assert.StartsWith("3.1.", openApiVersion, StringComparison.Ordinal);
        Assert.Contains("\"/pulse\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/widgets\"", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Swashbuckle", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NSwag", document, StringComparison.OrdinalIgnoreCase);
    }
}
