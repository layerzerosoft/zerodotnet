namespace LayerZero.Core;

/// <summary>
/// Describes a machine-readable application error.
/// </summary>
public sealed class Error : IEquatable<Error>
{
    /// <summary>
    /// Initializes a new <see cref="Error"/>.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="target">Optional field, parameter, or resource targeted by the error.</param>
    public Error(string code, string message, string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        Target = string.IsNullOrWhiteSpace(target) ? null : target;
    }

    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional field, parameter, or resource targeted by the error.
    /// </summary>
    public string? Target { get; }

    /// <summary>
    /// Creates a new <see cref="Error"/>.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="target">Optional field, parameter, or resource targeted by the error.</param>
    /// <returns>The created error.</returns>
    public static Error Create(string code, string message, string? target = null) => new(code, message, target);

    /// <inheritdoc />
    public bool Equals(Error? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Code, other.Code)
            && StringComparer.Ordinal.Equals(Message, other.Message)
            && StringComparer.Ordinal.Equals(Target, other.Target);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Error);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Code),
        StringComparer.Ordinal.GetHashCode(Message),
        Target is null ? 0 : StringComparer.Ordinal.GetHashCode(Target));

    /// <inheritdoc />
    public override string ToString() => Target is null ? $"{Code}: {Message}" : $"{Code} ({Target}): {Message}";
}
