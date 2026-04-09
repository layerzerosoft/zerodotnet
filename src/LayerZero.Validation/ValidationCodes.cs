namespace LayerZero.Validation;

/// <summary>
/// Contains standard validation error codes emitted by LayerZero validators.
/// </summary>
public static class ValidationCodes
{
    /// <summary>
    /// The value must not be null.
    /// </summary>
    public const string NotNull = "layerzero.validation.not_null";

    /// <summary>
    /// The value must not be empty.
    /// </summary>
    public const string NotEmpty = "layerzero.validation.not_empty";

    /// <summary>
    /// The value must not be longer than the configured maximum.
    /// </summary>
    public const string MaximumLength = "layerzero.validation.maximum_length";

    /// <summary>
    /// The value must not be shorter than the configured minimum.
    /// </summary>
    public const string MinimumLength = "layerzero.validation.minimum_length";

    /// <summary>
    /// The value must satisfy a custom predicate.
    /// </summary>
    public const string Must = "layerzero.validation.must";
}
