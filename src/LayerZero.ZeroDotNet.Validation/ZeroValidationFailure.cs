namespace LayerZero.ZeroDotNet.Validation;

/// <summary>
/// Describes one validation failure.
/// </summary>
public sealed class ZeroValidationFailure
{
    /// <summary>
    /// Initializes a new <see cref="ZeroValidationFailure"/>.
    /// </summary>
    /// <param name="propertyName">The property or parameter name.</param>
    /// <param name="code">The stable machine-readable validation code.</param>
    /// <param name="message">The human-readable validation message.</param>
    /// <param name="attemptedValue">The attempted value when it is useful to report.</param>
    public ZeroValidationFailure(string propertyName, string code, string message, object? attemptedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        PropertyName = propertyName;
        Code = code;
        Message = message;
        AttemptedValue = attemptedValue;
    }

    /// <summary>
    /// Gets the property or parameter name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the stable machine-readable validation code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable validation message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the attempted value when it is useful to report.
    /// </summary>
    public object? AttemptedValue { get; }

    /// <summary>
    /// Converts the validation failure to a core error.
    /// </summary>
    /// <returns>The equivalent core error.</returns>
    public ZeroError ToError() => new(Code, Message, PropertyName);
}
