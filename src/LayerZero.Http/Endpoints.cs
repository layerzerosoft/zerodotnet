using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LayerZero.Http;

internal sealed record EndpointDescriptor<TRequest>(
    HttpMethod Method,
    string Template,
    IReadOnlyList<ValueBinding<TRequest>> RouteBindings,
    IReadOnlyList<ValueBinding<TRequest>> QueryBindings,
    IReadOnlyList<ValueBinding<TRequest>> HeaderBindings,
    JsonBodyBinding<TRequest>? JsonBody);

internal sealed record ValueBinding<TRequest>(string Name, Func<TRequest, object?> Selector);

internal abstract class JsonBodyBinding<TRequest>
{
    public abstract HttpContent? CreateContent(TRequest request, JsonSerializerContext serializerContext);
}

internal sealed class JsonBodyBinding<TRequest, TBody> : JsonBodyBinding<TRequest>
{
    private readonly Func<TRequest, TBody?> selector;

    public JsonBodyBinding(Func<TRequest, TBody?> selector)
    {
        this.selector = selector;
    }

    public override HttpContent? CreateContent(TRequest request, JsonSerializerContext serializerContext)
    {
        TBody? value = selector(request);
        if (value is null)
        {
            return null;
        }

        JsonTypeInfo<TBody> typeInfo = serializerContext.GetTypeInfo(typeof(TBody)) as JsonTypeInfo<TBody>
            ?? throw new InvalidOperationException($"JSON metadata for '{typeof(TBody).FullName}' is unavailable.");

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        ByteArrayContent content = new(payload);
        content.Headers.ContentType = new("application/json")
        {
            CharSet = Encoding.UTF8.WebName,
        };

        return content;
    }
}

/// <summary>
/// Describes a typed HTTP endpoint contract without a success payload.
/// </summary>
/// <typeparam name="TSelf">Concrete endpoint type.</typeparam>
/// <typeparam name="TRequest">Request contract type.</typeparam>
public abstract class Endpoint<TSelf, TRequest>
    where TSelf : Endpoint<TSelf, TRequest>
{
    private protected Endpoint(EndpointDescriptor<TRequest> descriptor)
    {
        Descriptor = descriptor;
    }

    internal EndpointDescriptor<TRequest> Descriptor { get; }

    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public HttpMethod Method => Descriptor.Method;

    /// <summary>
    /// Gets the route template.
    /// </summary>
    public string Template => Descriptor.Template;

    /// <summary>
    /// Adds a route value binding.
    /// </summary>
    public TSelf Route(string name, Func<TRequest, object?> selector)
    {
        return Create(Descriptor with
        {
            RouteBindings = AddBinding(Descriptor.RouteBindings, name, selector),
        });
    }

    /// <summary>
    /// Adds a query parameter binding.
    /// </summary>
    public TSelf Query(string name, Func<TRequest, object?> selector)
    {
        return Create(Descriptor with
        {
            QueryBindings = AddBinding(Descriptor.QueryBindings, name, selector),
        });
    }

    /// <summary>
    /// Adds an HTTP header binding.
    /// </summary>
    public TSelf Header(string name, Func<TRequest, object?> selector)
    {
        return Create(Descriptor with
        {
            HeaderBindings = AddBinding(Descriptor.HeaderBindings, name, selector),
        });
    }

    private protected abstract TSelf Create(EndpointDescriptor<TRequest> descriptor);

    private static IReadOnlyList<ValueBinding<TRequest>> AddBinding(
        IReadOnlyList<ValueBinding<TRequest>> bindings,
        string name,
        Func<TRequest, object?> selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selector);

        if (bindings.Any(binding => string.Equals(binding.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The HTTP contract already defines a binding named '{name}'.");
        }

        return [.. bindings, new ValueBinding<TRequest>(name, selector)];
    }

    internal static EndpointDescriptor<TRequest> CreateDescriptor(HttpMethod method, string template)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        if (!template.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("HTTP endpoint templates must start with '/'.", nameof(template));
        }

        return new EndpointDescriptor<TRequest>(
            method,
            template,
            [],
            [],
            [],
            JsonBody: null);
    }
}

/// <summary>
/// Describes a typed HTTP endpoint contract with a success payload.
/// </summary>
/// <typeparam name="TSelf">Concrete endpoint type.</typeparam>
/// <typeparam name="TRequest">Request contract type.</typeparam>
/// <typeparam name="TResponse">Success payload type.</typeparam>
public abstract class ResponseEndpoint<TSelf, TRequest, TResponse> : Endpoint<TSelf, TRequest>
    where TSelf : ResponseEndpoint<TSelf, TRequest, TResponse>
{
    private protected ResponseEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }
}

/// <summary>
/// Describes a body-capable HTTP endpoint contract without a success payload.
/// </summary>
/// <typeparam name="TSelf">Concrete endpoint type.</typeparam>
/// <typeparam name="TRequest">Request contract type.</typeparam>
public abstract class BodyEndpoint<TSelf, TRequest> : Endpoint<TSelf, TRequest>
    where TSelf : BodyEndpoint<TSelf, TRequest>
{
    private protected BodyEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    /// <summary>
    /// Adds a JSON body binding.
    /// </summary>
    public TSelf JsonBody<TBody>(Func<TRequest, TBody?> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Create(Descriptor with
        {
            JsonBody = new JsonBodyBinding<TRequest, TBody>(selector),
        });
    }
}

/// <summary>
/// Describes a body-capable HTTP endpoint contract with a success payload.
/// </summary>
/// <typeparam name="TSelf">Concrete endpoint type.</typeparam>
/// <typeparam name="TRequest">Request contract type.</typeparam>
/// <typeparam name="TResponse">Success payload type.</typeparam>
public abstract class BodyResponseEndpoint<TSelf, TRequest, TResponse> : ResponseEndpoint<TSelf, TRequest, TResponse>
    where TSelf : BodyResponseEndpoint<TSelf, TRequest, TResponse>
{
    private protected BodyResponseEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    /// <summary>
    /// Adds a JSON body binding.
    /// </summary>
    public TSelf JsonBody<TBody>(Func<TRequest, TBody?> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Create(Descriptor with
        {
            JsonBody = new JsonBodyBinding<TRequest, TBody>(selector),
        });
    }
}

/// <summary>
/// Describes a typed GET endpoint contract with a success payload.
/// </summary>
public sealed class GetEndpoint<TRequest, TResponse> : ResponseEndpoint<GetEndpoint<TRequest, TResponse>, TRequest, TResponse>
{
    internal GetEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Get, template))
    {
    }

    private GetEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override GetEndpoint<TRequest, TResponse> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed GET endpoint contract without a success payload.
/// </summary>
public sealed class GetEndpoint<TRequest> : Endpoint<GetEndpoint<TRequest>, TRequest>
{
    internal GetEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Get, template))
    {
    }

    private GetEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override GetEndpoint<TRequest> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed POST endpoint contract with a success payload.
/// </summary>
public sealed class PostEndpoint<TRequest, TResponse> : BodyResponseEndpoint<PostEndpoint<TRequest, TResponse>, TRequest, TResponse>
{
    internal PostEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Post, template))
    {
    }

    private PostEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PostEndpoint<TRequest, TResponse> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed POST endpoint contract without a success payload.
/// </summary>
public sealed class PostEndpoint<TRequest> : BodyEndpoint<PostEndpoint<TRequest>, TRequest>
{
    internal PostEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Post, template))
    {
    }

    private PostEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PostEndpoint<TRequest> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed PUT endpoint contract with a success payload.
/// </summary>
public sealed class PutEndpoint<TRequest, TResponse> : BodyResponseEndpoint<PutEndpoint<TRequest, TResponse>, TRequest, TResponse>
{
    internal PutEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Put, template))
    {
    }

    private PutEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PutEndpoint<TRequest, TResponse> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed PUT endpoint contract without a success payload.
/// </summary>
public sealed class PutEndpoint<TRequest> : BodyEndpoint<PutEndpoint<TRequest>, TRequest>
{
    internal PutEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Put, template))
    {
    }

    private PutEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PutEndpoint<TRequest> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed PATCH endpoint contract with a success payload.
/// </summary>
public sealed class PatchEndpoint<TRequest, TResponse> : BodyResponseEndpoint<PatchEndpoint<TRequest, TResponse>, TRequest, TResponse>
{
    internal PatchEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Patch, template))
    {
    }

    private PatchEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PatchEndpoint<TRequest, TResponse> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed PATCH endpoint contract without a success payload.
/// </summary>
public sealed class PatchEndpoint<TRequest> : BodyEndpoint<PatchEndpoint<TRequest>, TRequest>
{
    internal PatchEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Patch, template))
    {
    }

    private PatchEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override PatchEndpoint<TRequest> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed DELETE endpoint contract with a success payload.
/// </summary>
public sealed class DeleteEndpoint<TRequest, TResponse> : ResponseEndpoint<DeleteEndpoint<TRequest, TResponse>, TRequest, TResponse>
{
    internal DeleteEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Delete, template))
    {
    }

    private DeleteEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override DeleteEndpoint<TRequest, TResponse> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}

/// <summary>
/// Describes a typed DELETE endpoint contract without a success payload.
/// </summary>
public sealed class DeleteEndpoint<TRequest> : Endpoint<DeleteEndpoint<TRequest>, TRequest>
{
    internal DeleteEndpoint(string template)
        : this(CreateDescriptor(HttpMethod.Delete, template))
    {
    }

    private DeleteEndpoint(EndpointDescriptor<TRequest> descriptor)
        : base(descriptor)
    {
    }

    private protected override DeleteEndpoint<TRequest> Create(EndpointDescriptor<TRequest> descriptor) => new(descriptor);
}
