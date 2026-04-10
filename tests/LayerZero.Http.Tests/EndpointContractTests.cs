using LayerZero.Http;

namespace LayerZero.Http.Tests;

public sealed class EndpointContractTests
{
    [Fact]
    public void Endpoint_factories_capture_http_method_and_template()
    {
        GetEndpoint<Request, Response> endpoint = HttpEndpoint
            .Get<Request, Response>("/todos/{id:guid}")
            .Route("id", static request => request.Id)
            .Query("includeCompleted", static request => request.IncludeCompleted);

        Assert.Equal(HttpMethod.Get, endpoint.Method);
        Assert.Equal("/todos/{id:guid}", endpoint.Template);
    }

    [Fact]
    public void Endpoint_configuration_is_immutable()
    {
        GetEndpoint<Request, Response> baseline = HttpEndpoint.Get<Request, Response>("/todos");
        GetEndpoint<Request, Response> configured = baseline.Query("includeCompleted", static request => request.IncludeCompleted);

        Assert.Equal("/todos", baseline.Template);
        Assert.Equal("/todos", configured.Template);
        Assert.NotSame(baseline, configured);
    }

    [Fact]
    public void Get_endpoints_do_not_expose_json_body_configuration()
    {
        Assert.DoesNotContain(
            typeof(GetEndpoint<Request, Response>).GetMethods().Select(static method => method.Name),
            static name => string.Equals(name, "JsonBody", StringComparison.Ordinal));
    }

    [Fact]
    public void Post_endpoints_expose_json_body_configuration()
    {
        Assert.Contains(
            typeof(PostEndpoint<Request, Response>).GetMethods().Select(static method => method.Name),
            static name => string.Equals(name, "JsonBody", StringComparison.Ordinal));
    }

    private sealed record Request(Guid Id, bool? IncludeCompleted, RequestBody Body);

    private sealed record RequestBody(string Title);

    private sealed record Response(Guid Id);
}
