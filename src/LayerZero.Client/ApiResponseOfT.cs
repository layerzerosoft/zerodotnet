using System.Net;
using LayerZero.Core;

namespace LayerZero.Client;

/// <summary>
/// Represents an API response with a success payload.
/// </summary>
/// <typeparam name="T">The success payload type.</typeparam>
public sealed class ApiResponse<T>
{
    /// <summary>
    /// Initializes a new <see cref="ApiResponse{T}"/>.
    /// </summary>
    public ApiResponse(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contentHeaders,
        Result<T> result,
        ApiProblemDetails? problem)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(contentHeaders);
        ArgumentNullException.ThrowIfNull(result);

        StatusCode = statusCode;
        Headers = headers;
        ContentHeaders = contentHeaders;
        Result = result;
        Problem = problem;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    /// <summary>
    /// Gets the response content headers.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ContentHeaders { get; }

    /// <summary>
    /// Gets the LayerZero result view of the response.
    /// </summary>
    public Result<T> Result { get; }

    /// <summary>
    /// Gets the parsed problem payload when the API responded with problem details.
    /// </summary>
    public ApiProblemDetails? Problem { get; }

    /// <summary>
    /// Gets whether the API call succeeded.
    /// </summary>
    public bool IsSuccess => Result.IsSuccess;

    /// <summary>
    /// Gets whether the API call failed.
    /// </summary>
    public bool IsFailure => Result.IsFailure;
}
