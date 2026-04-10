using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LayerZero.Core;
using LayerZero.MinimalApi.Features.Todos.Events;
using LayerZero.MinimalApi.Contracts.Todos;
using LayerZero.Validation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.AspNetCore.Tests;

public sealed class MinimalApiSampleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public MinimalApiSampleTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Todo_endpoints_are_mapped_by_generated_slice_extensions()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            "/todos",
            new { title = "  Draft slice mechanics  ", dueOn = "2026-04-10" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/todos/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Assert.Equal("Draft slice mechanics", body["title"]?.GetValue<string>());
        Assert.False(body["isCompleted"]?.GetValue<bool>());
    }

    [Fact]
    public void Generated_add_slices_registers_handlers_validators_and_event_handlers()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetService<IAsyncRequestHandler<CreateTodo.Request, Todo>>());
        Assert.NotNull(services.GetService<IAsyncRequestHandler<GetTodo.Request, Todo>>());
        Assert.NotEmpty(services.GetServices<IValidator<CreateTodo.Request>>());
        Assert.NotEmpty(services.GetServices<IEventHandler<TodoCreated>>());
    }

    [Fact]
    public async Task Validation_returns_problem_details_with_layerzero_errors()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            "/todos",
            new { title = "" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Assert.Equal("Validation failed.", body["title"]?.GetValue<string>());
        Assert.NotNull(body["errors"]?["Title"]);
        Assert.NotNull(body["layerzero.errors"]);
    }

    [Fact]
    public async Task Native_minimal_api_binding_services_links_and_query_values_still_work()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var todoId = await CreateTodoAsync(client, "Complete the generated slice", cancellationToken);

        var todo = await client.GetFromJsonAsync<JsonObject>($"/todos/{todoId}", cancellationToken)
            ?? throw new InvalidOperationException("Todo response was empty.");
        Assert.Equal(todoId, todo["id"]?.GetValue<Guid>());

        var completeResponse = await client.PostAsync($"/todos/{todoId}/complete", null, cancellationToken);
        completeResponse.EnsureSuccessStatusCode();

        var completed = (await completeResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Assert.True(completed["isCompleted"]?.GetValue<bool>());

        var defaultTodos = await client.GetFromJsonAsync<JsonArray>("/todos", cancellationToken)
            ?? throw new InvalidOperationException("Default todo response was empty.");
        Assert.DoesNotContain(defaultTodos, node => HasId(node, todoId));

        var activeTodos = await client.GetFromJsonAsync<JsonArray>("/todos?includeCompleted=false", cancellationToken)
            ?? throw new InvalidOperationException("Active todo response was empty.");
        Assert.DoesNotContain(activeTodos, node => HasId(node, todoId));

        var allTodos = await client.GetFromJsonAsync<JsonArray>("/todos?includeCompleted=true", cancellationToken)
            ?? throw new InvalidOperationException("All todo response was empty.");
        Assert.Contains(allTodos, node => HasId(node, todoId));

        var invalidQueryResponse = await client.GetAsync("/todos?includeCompleted=abc", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidQueryResponse.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_includes_self_mapped_endpoints_without_swashbuckle_or_nswag()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var document = await client.GetStringAsync("/openapi/v1.json", cancellationToken);
        var openApi = JsonNode.Parse(document)!.AsObject();

        var openApiVersion = openApi["openapi"]?.GetValue<string>();
        Assert.NotNull(openApiVersion);
        Assert.StartsWith("3.1.", openApiVersion, StringComparison.Ordinal);
        Assert.Contains("\"/todos\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/todos/{id}\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/todos/{id}/complete\"", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Swashbuckle", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NSwag", document, StringComparison.OrdinalIgnoreCase);

        var parameters = openApi["paths"]?["/todos"]?["get"]?["parameters"]?.AsArray()
            ?? throw new InvalidOperationException("Todo list parameters were missing from OpenAPI.");
        var includeCompleted = parameters
            .Select(parameter => parameter?.AsObject())
            .FirstOrDefault(parameter => parameter?["name"]?.GetValue<string>() == "includeCompleted")
            ?? throw new InvalidOperationException("includeCompleted parameter was missing from OpenAPI.");

        Assert.Equal("query", includeCompleted["in"]?.GetValue<string>());
        Assert.False(includeCompleted["required"]?.GetValue<bool>() ?? false);
    }

    private static async Task<Guid> CreateTodoAsync(
        HttpClient client,
        string title,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/todos",
            new { title },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        return body["id"]!.GetValue<Guid>();
    }

    private static bool HasId(JsonNode? node, Guid id)
    {
        return node is JsonObject todo && todo["id"]?.GetValue<Guid>() == id;
    }
}
