using System.Net;
using LayerZero.Client;
using LayerZero.MinimalApi.Client.Sample.Clients;
using LayerZero.MinimalApi.Contracts.Todos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Client.Integration.Tests;

public sealed class TypedClientIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public TypedClientIntegrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Explicit_typed_client_handles_create_get_complete_and_list_flows()
    {
        TodosClient client = CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var created = await client.CreateAsync(new CreateTodo.Request("Ship explicit LayerZero client", null), cancellationToken);

        Assert.True(created.IsSuccess);

        var fetched = await client.GetAsync(created.Value.Id, cancellationToken);
        Assert.True(fetched.IsSuccess);
        Assert.Equal(created.Value.Id, fetched.Value.Id);

        var completed = await client.CompleteAsync(created.Value.Id, cancellationToken);
        Assert.True(completed.IsSuccess);
        Assert.True(completed.Value.IsCompleted);

        var active = await client.ListAsync(cancellationToken: cancellationToken);
        Assert.True(active.IsSuccess);
        Assert.DoesNotContain(active.Value, todo => todo.Id == created.Value.Id);

        var all = await client.ListAsync(includeCompleted: true, cancellationToken: cancellationToken);
        Assert.True(all.IsSuccess);
        Assert.Contains(all.Value, todo => todo.Id == created.Value.Id);
    }

    [Fact]
    public async Task Explicit_typed_client_maps_validation_failures_to_layerzero_results()
    {
        TodosClient client = CreateClient();

        var response = await client.CreateForResponseAsync(
            new CreateTodo.Request(string.Empty, null),
            TestContext.Current.CancellationToken);

        Assert.True(response.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(response.Problem);
        Assert.Contains(response.Result.Errors, error => error.Code == "layerzero.validation.not_empty");
    }

    [Fact]
    public async Task Explicit_typed_client_maps_not_found_without_throwing_and_exposes_headers()
    {
        TodosClient client = CreateClient();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var createdResponse = await client.CreateForResponseAsync(
            new CreateTodo.Request("Inspect headers", null),
            cancellationToken);

        Assert.True(createdResponse.IsSuccess);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.True(createdResponse.Headers.ContainsKey("Location"));

        Guid missingId = Guid.NewGuid();
        var missing = await client.GetForResponseAsync(missingId, cancellationToken);

        Assert.True(missing.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(missing.Result.Errors, error => error.Code == "layerzero.http.status.404");
        Assert.Null(missing.Problem);
    }

    [Fact]
    public void Typed_client_registration_keeps_ihttpclientbuilder_chaining_available()
    {
        ServiceCollection services = new();
        services.AddTransient<PassthroughHandler>();

        var builder = services.AddLayerZeroClient<TodosClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7270");
        }).AddHttpMessageHandler<PassthroughHandler>();

        Assert.NotNull(builder);
    }

    private TodosClient CreateClient()
    {
        return new TodosClient(factory.CreateClient());
    }

    private sealed class PassthroughHandler : DelegatingHandler;
}
