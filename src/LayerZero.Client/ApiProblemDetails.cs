using System.Text.Json;
using LayerZero.Core;

namespace LayerZero.Client;

/// <summary>
/// Describes a problem-details response returned by a LayerZero client call.
/// </summary>
public sealed class ApiProblemDetails
{
    /// <summary>
    /// Initializes a new <see cref="ApiProblemDetails"/>.
    /// </summary>
    public ApiProblemDetails(
        string? type,
        string? title,
        int? status,
        string? detail,
        string? instance,
        IReadOnlyList<Error> errors,
        JsonElement raw)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Type = type;
        Title = title;
        Status = status;
        Detail = detail;
        Instance = instance;
        Errors = errors;
        Raw = raw;
    }

    /// <summary>
    /// Gets the problem type URI.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the problem title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the HTTP status code carried by the problem details payload.
    /// </summary>
    public int? Status { get; }

    /// <summary>
    /// Gets the problem detail text.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets the problem instance URI.
    /// </summary>
    public string? Instance { get; }

    /// <summary>
    /// Gets the normalized LayerZero errors derived from the problem payload.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>
    /// Gets the raw JSON payload for advanced callers.
    /// </summary>
    public JsonElement Raw { get; }
}
