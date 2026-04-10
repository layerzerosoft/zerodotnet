using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LayerZero.Core;
using LayerZero.Http;

namespace LayerZero.Client.Tests;

public sealed partial class LayerZeroClientTests
{
    [Fact]
    public async Task Contracts_build_expected_path_headers_and_body()
    {
        HttpRequestMessage? capturedRequest = null;
        LayerZeroClient client = CreateClient(
            request =>
            {
                capturedRequest = Clone(request);
                return Json(HttpStatusCode.OK, """{"value":"ok"}""");
            });

        PostEndpoint<RequestEnvelope, Payload> endpoint = HttpEndpoint
            .Post<RequestEnvelope, Payload>("/todos/{id:guid}")
            .Route("id", static request => request.Id)
            .Query("includeCompleted", static request => request.IncludeCompleted)
            .Header("x-correlation-id", static request => request.CorrelationId)
            .JsonBody(static request => request.Body);

        Result<Payload> result = await client.SendAsync(
            endpoint,
            new RequestEnvelope(
                Guid.Parse("3f1f5542-6c60-4cfc-b2a4-2cf9f36f8c1a"),
                IncludeCompleted: true,
                CorrelationId: "corr-42",
                new RequestBody("Ship LayerZero")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://localhost/todos/3f1f5542-6c60-4cfc-b2a4-2cf9f36f8c1a?includeCompleted=true", capturedRequest.RequestUri?.ToString());
        Assert.Equal("corr-42", capturedRequest.Headers.GetValues("x-correlation-id").Single());
        Assert.Equal("application/json; charset=utf-8", capturedRequest.Content?.Headers.ContentType?.ToString());

        string payload = await capturedRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"title\":\"Ship LayerZero\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nullable_query_values_are_omitted()
    {
        HttpRequestMessage? capturedRequest = null;
        LayerZeroClient client = CreateClient(
            request =>
            {
                capturedRequest = Clone(request);
                return Json(HttpStatusCode.OK, """{"value":"ok"}""");
            });

        GetEndpoint<ListRequest, Payload> endpoint = HttpEndpoint
            .Get<ListRequest, Payload>("/todos")
            .Query("includeCompleted", static request => request.IncludeCompleted);

        Result<Payload> result = await client.SendAsync(endpoint, new ListRequest(IncludeCompleted: null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://localhost/todos", capturedRequest!.RequestUri?.ToString());
    }

    [Fact]
    public async Task Problem_details_with_layerzero_errors_map_to_failed_results()
    {
        LayerZeroClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """
                {
                  "title": "Validation failed.",
                  "status": 400,
                  "layerzero.errors": [
                    {
                      "code": "layerzero.validation.not_empty",
                      "propertyName": "Title",
                      "message": "Title must not be empty."
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/problem+json"),
        });

        Result<Payload> result = await client.SendAsync(
            HttpEndpoint.Get<LayerZero.Core.Unit, Payload>("/payload"),
            Unit.Value,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "layerzero.validation.not_empty" && error.Target == "Title");
    }

    [Fact]
    public async Task Advanced_responses_expose_status_and_headers()
    {
        LayerZeroClient client = CreateClient(_ =>
        {
            HttpResponseMessage response = Json(HttpStatusCode.Created, """{"value":"ok"}""");
            response.Headers.Location = new Uri("/todos/42", UriKind.Relative);
            return response;
        });

        ApiResponse<Payload> response = await client.SendForResponseAsync(
            HttpEndpoint.Get<LayerZero.Core.Unit, Payload>("/payload"),
            Unit.Value,
            TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/todos/42", response.Headers["Location"].Single());
    }

    [Fact]
    public async Task Transport_failures_remain_native_exceptions()
    {
        LayerZeroClient client = CreateClient(_ => throw new HttpRequestException("network down"));

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            _ = await client.SendAsync(
                HttpEndpoint.Get<LayerZero.Core.Unit, Payload>("/payload"),
                Unit.Value,
                TestContext.Current.CancellationToken);
        });
    }

    private static LayerZeroClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        HttpClient httpClient = new(new StubHandler(handler))
        {
            BaseAddress = new Uri("https://localhost"),
        };

        return new LayerZeroClient(httpClient, TestJsonContext.Default);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri);

        foreach ((string key, IEnumerable<string> values) in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(key, values);
        }

        if (request.Content is not null)
        {
            string content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clone.Content = new StringContent(
                content,
                Encoding.UTF8,
                request.Content.Headers.ContentType?.MediaType ?? "application/json");

            foreach ((string key, IEnumerable<string> values) in request.Content.Headers)
            {
                clone.Content.Headers.Remove(key);
                clone.Content.Headers.TryAddWithoutValidation(key, values);
            }
        }

        return clone;
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed record RequestEnvelope(Guid Id, bool? IncludeCompleted, string CorrelationId, RequestBody Body);

    private sealed record ListRequest(bool? IncludeCompleted);

    private sealed record RequestBody(string Title);

    private sealed record Payload(string Value);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(Payload))]
    [JsonSerializable(typeof(RequestBody))]
    private sealed partial class TestJsonContext : JsonSerializerContext
    {
    }
}
