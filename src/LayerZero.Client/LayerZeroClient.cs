using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LayerZero.Core;
using LayerZero.Http;

namespace LayerZero.Client;

/// <summary>
/// Sends strongly typed LayerZero HTTP contracts over <see cref="HttpClient"/>.
/// </summary>
public sealed class LayerZeroClient
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyHeaders =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerContext serializerContext;

    /// <summary>
    /// Initializes a new <see cref="LayerZeroClient"/>.
    /// </summary>
    public LayerZeroClient(HttpClient httpClient, JsonSerializerContext serializerContext)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.serializerContext = serializerContext ?? throw new ArgumentNullException(nameof(serializerContext));
    }

    /// <summary>
    /// Gets the underlying HTTP client.
    /// </summary>
    public HttpClient HttpClient { get; }

    /// <summary>
    /// Sends a typed HTTP contract that does not return a success payload.
    /// </summary>
    public async ValueTask<Result> SendAsync<TEndpoint, TRequest>(
        Endpoint<TEndpoint, TRequest> endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TEndpoint : Endpoint<TEndpoint, TRequest>
    {
        var response = await SendForResponseAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
        return response.Result;
    }

    /// <summary>
    /// Sends a typed HTTP contract that returns a success payload.
    /// </summary>
    public async ValueTask<Result<TResponse>> SendAsync<TEndpoint, TRequest, TResponse>(
        ResponseEndpoint<TEndpoint, TRequest, TResponse> endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TEndpoint : ResponseEndpoint<TEndpoint, TRequest, TResponse>
    {
        var response = await SendForResponseAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
        return response.Result;
    }

    /// <summary>
    /// Sends a typed HTTP contract and returns advanced response metadata.
    /// </summary>
    public async ValueTask<ApiResponse> SendForResponseAsync<TEndpoint, TRequest>(
        Endpoint<TEndpoint, TRequest> endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TEndpoint : Endpoint<TEndpoint, TRequest>
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var requestMessage = CreateRequest(endpoint.Descriptor, request);
        using var response = await HttpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var headers = CaptureHeaders(response);

        if (response.IsSuccessStatusCode)
        {
            return new ApiResponse(
                response.StatusCode,
                headers.Headers,
                headers.ContentHeaders,
                Result.Success(),
                problem: null);
        }

        var problem = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse(
            response.StatusCode,
            headers.Headers,
            headers.ContentHeaders,
            Result.Failure(GetErrors(response.StatusCode, problem)),
            problem);
    }

    /// <summary>
    /// Sends a typed HTTP contract and returns advanced response metadata.
    /// </summary>
    public async ValueTask<ApiResponse<TResponse>> SendForResponseAsync<TEndpoint, TRequest, TResponse>(
        ResponseEndpoint<TEndpoint, TRequest, TResponse> endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TEndpoint : ResponseEndpoint<TEndpoint, TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var requestMessage = CreateRequest(endpoint.Descriptor, request);
        using var response = await HttpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var headers = CaptureHeaders(response);

        if (response.IsSuccessStatusCode)
        {
            var payload = await ReadContentBytesAsync(response.Content, cancellationToken).ConfigureAwait(false);
            if (payload.Length == 0 || response.StatusCode == HttpStatusCode.NoContent)
            {
                return new ApiResponse<TResponse>(
                    response.StatusCode,
                    headers.Headers,
                    headers.ContentHeaders,
                    Result<TResponse>.Failure(CreateEmptyResponseError()),
                    problem: null);
            }

            var responseTypeInfo = GetTypeInfo<TResponse>();
            var value = JsonSerializer.Deserialize(payload, responseTypeInfo);

            if (value is null)
            {
                return new ApiResponse<TResponse>(
                    response.StatusCode,
                    headers.Headers,
                    headers.ContentHeaders,
                    Result<TResponse>.Failure(CreateEmptyResponseError()),
                    problem: null);
            }

            return new ApiResponse<TResponse>(
                response.StatusCode,
                headers.Headers,
                headers.ContentHeaders,
                Result<TResponse>.Success(value),
                problem: null);
        }

        var problem = await ReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse<TResponse>(
            response.StatusCode,
            headers.Headers,
            headers.ContentHeaders,
            Result<TResponse>.Failure(GetErrors(response.StatusCode, problem)),
            problem);
    }

    private HttpRequestMessage CreateRequest<TRequest>(
        EndpointDescriptor<TRequest> descriptor,
        TRequest request)
    {
        var path = ResolvePath(descriptor, request);
        var uriBuilder = new StringBuilder(path);
        var hasQuery = false;

        foreach (var binding in descriptor.QueryBindings)
        {
            AppendQueryParameter(uriBuilder, binding.Name, binding.Selector(request), ref hasQuery);
        }

        var message = new HttpRequestMessage(descriptor.Method, uriBuilder.ToString());

        foreach (var binding in descriptor.HeaderBindings)
        {
            AddHeader(message, binding.Name, binding.Selector(request));
        }

        message.Content = descriptor.JsonBody?.CreateContent(request, serializerContext);
        return message;
    }

    private static string ResolvePath<TRequest>(EndpointDescriptor<TRequest> descriptor, TRequest request)
    {
        if (descriptor.RouteBindings.Count == 0)
        {
            return descriptor.Template;
        }

        var routeValues = descriptor.RouteBindings.ToDictionary(
            binding => binding.Name,
            binding => binding.Selector(request),
            StringComparer.Ordinal);

        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder(descriptor.Template.Length + 32);

        for (var index = 0; index < descriptor.Template.Length;)
        {
            if (descriptor.Template[index] != '{')
            {
                builder.Append(descriptor.Template[index]);
                index++;
                continue;
            }

            var tokenEnd = descriptor.Template.IndexOf('}', index + 1);
            if (tokenEnd < 0)
            {
                throw new InvalidOperationException($"HTTP route template '{descriptor.Template}' is invalid.");
            }

            var token = descriptor.Template[(index + 1)..tokenEnd];
            var separator = token.IndexOf(':');
            var name = separator >= 0 ? token[..separator] : token;

            if (!routeValues.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException(
                    $"HTTP route template '{descriptor.Template}' requires a route value named '{name}'.");
            }

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"HTTP route value '{name}' cannot be null for template '{descriptor.Template}'.");
            }

            consumed.Add(name);
            builder.Append(Uri.EscapeDataString(FormatValue(value)));
            index = tokenEnd + 1;
        }

        var unused = routeValues.Keys.FirstOrDefault(name => !consumed.Contains(name));
        if (unused is not null)
        {
            throw new InvalidOperationException(
                $"HTTP contract declares route value '{unused}', but template '{descriptor.Template}' does not use it.");
        }

        return builder.ToString();
    }

    private JsonTypeInfo<T> GetTypeInfo<T>()
    {
        return serializerContext.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"JSON metadata for '{typeof(T).FullName}' is unavailable.");
    }

    private static string FormatValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            bool boolean => boolean ? "true" : "false",
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            Enum enumValue => enumValue.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static void AppendQueryParameter(
        StringBuilder builder,
        string name,
        object? value,
        ref bool hasQuery)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (value is null)
        {
            return;
        }

        builder.Append(hasQuery ? '&' : '?');
        hasQuery = true;
        builder.Append(Uri.EscapeDataString(name));
        builder.Append('=');
        builder.Append(Uri.EscapeDataString(FormatValue(value)));
    }

    private static void AddHeader(
        HttpRequestMessage request,
        string name,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (value is null)
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(name, FormatValue(value));
    }

    private static async ValueTask<byte[]> ReadContentBytesAsync(
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return [];
        }

        return await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HeaderSnapshot CaptureHeaders(HttpResponseMessage response)
    {
        return new HeaderSnapshot(
            SnapshotHeaders(response.Headers),
            response.Content is null ? EmptyHeaders : SnapshotHeaders(response.Content.Headers));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> SnapshotHeaders(HttpHeaders headers)
    {
        var snapshot = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            snapshot[header.Key] = header.Value.ToArray();
        }

        return snapshot;
    }

    private static Error CreateEmptyResponseError()
    {
        return Error.Create(
            "layerzero.http.empty-response",
            "The API returned an empty response body for an operation that expects a payload.");
    }

    private static IReadOnlyList<Error> GetErrors(HttpStatusCode statusCode, ApiProblemDetails? problem)
    {
        if (problem is not null)
        {
            return problem.Errors;
        }

        return
        [
            Error.Create(
                $"layerzero.http.status.{(int)statusCode}",
                $"The API responded with HTTP {(int)statusCode}."),
        ];
    }

    private static async ValueTask<ApiProblemDetails?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = await ReadContentBytesAsync(response.Content, cancellationToken).ConfigureAwait(false);
        if (payload.Length == 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement.Clone();

        var type = GetString(root, "type");
        var title = GetString(root, "title");
        var detail = GetString(root, "detail");
        var instance = GetString(root, "instance");
        int? status = GetInt32(root, "status") ?? (int)response.StatusCode;

        var errors = ReadLayerZeroErrors(root);
        if (errors.Count == 0)
        {
            errors.Add(Error.Create(
                status is int value
                    ? $"layerzero.http.problem.{value}"
                    : "layerzero.http.problem",
                detail ?? title ?? $"The API responded with HTTP {(int)response.StatusCode}."));
        }

        return new ApiProblemDetails(
            type,
            title,
            status,
            detail,
            instance,
            errors,
            root);
    }

    private static List<Error> ReadLayerZeroErrors(JsonElement root)
    {
        if (!TryGetProperty(root, "layerzero.errors", out var errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var errors = new List<Error>();

        foreach (var item in errorsElement.EnumerateArray())
        {
            var code = GetString(item, "code");
            var message = GetString(item, "message");
            var target = GetString(item, "target") ?? GetString(item, "propertyName");

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            errors.Add(Error.Create(code, message, target));
        }

        return errors;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value)
            && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private readonly record struct HeaderSnapshot(
        IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ContentHeaders);
}
