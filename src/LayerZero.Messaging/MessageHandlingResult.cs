using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Messaging;

/// <summary>
/// Represents the outcome of invoking validators and handlers for one message.
/// </summary>
public sealed class MessageHandlingResult
{
    private MessageHandlingResult(ValidationResult? validation, Result? result)
    {
        Validation = validation;
        Result = result;
    }

    /// <summary>
    /// Gets the validation result when validation failed.
    /// </summary>
    public ValidationResult? Validation { get; }

    /// <summary>
    /// Gets the result when a handler completed.
    /// </summary>
    public Result? Result { get; }

    /// <summary>
    /// Gets whether handling completed successfully.
    /// </summary>
    public bool IsSuccess => Validation is null && (Result?.IsSuccess ?? true);

    /// <summary>
    /// Gets whether validation failed.
    /// </summary>
    public bool IsValidationFailure => Validation?.IsInvalid == true;

    /// <summary>
    /// Gets whether a handler returned a failed result.
    /// </summary>
    public bool IsHandlerFailure => Result?.IsFailure == true;

    /// <summary>
    /// Creates a successful handling result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static MessageHandlingResult Success() => new(null, LayerZero.Core.Result.Success());

    /// <summary>
    /// Creates a validation failure result.
    /// </summary>
    /// <param name="validation">The validation result.</param>
    /// <returns>The failed result.</returns>
    public static MessageHandlingResult ValidationFailure(ValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return new MessageHandlingResult(validation, null);
    }

    /// <summary>
    /// Creates a handler result.
    /// </summary>
    /// <param name="result">The handler result.</param>
    /// <returns>The wrapped result.</returns>
    public static MessageHandlingResult FromResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new MessageHandlingResult(null, result);
    }
}
