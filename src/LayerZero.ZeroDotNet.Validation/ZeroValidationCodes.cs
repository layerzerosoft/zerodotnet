namespace LayerZero.ZeroDotNet.Validation;

/// <summary>
/// Contains standard validation error codes emitted by ZeroDotNet validators.
/// </summary>
public static class ZeroValidationCodes
{
    /// <summary>
    /// The value must not be null.
    /// </summary>
    public const string NotNull = "zero.validation.not_null";

    /// <summary>
    /// The value must not be empty.
    /// </summary>
    public const string NotEmpty = "zero.validation.not_empty";

    /// <summary>
    /// The value must not be longer than the configured maximum.
    /// </summary>
    public const string MaximumLength = "zero.validation.maximum_length";

    /// <summary>
    /// The value must not be shorter than the configured minimum.
    /// </summary>
    public const string MinimumLength = "zero.validation.minimum_length";

    /// <summary>
    /// The value must satisfy a custom predicate.
    /// </summary>
    public const string Must = "zero.validation.must";
}
