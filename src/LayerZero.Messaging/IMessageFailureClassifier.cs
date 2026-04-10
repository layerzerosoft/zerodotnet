using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Messaging;

/// <summary>
/// Classifies message failures into retryable or terminal outcomes.
/// </summary>
public interface IMessageFailureClassifier
{
    /// <summary>
    /// Classifies validation failures.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="validation">The validation result.</param>
    /// <returns>The failure action.</returns>
    MessageFailureAction ClassifyValidationFailure(MessageContext context, ValidationResult validation);

    /// <summary>
    /// Classifies result failures.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="result">The handler result.</param>
    /// <returns>The failure action.</returns>
    MessageFailureAction ClassifyResultFailure(MessageContext context, Result result);

    /// <summary>
    /// Classifies exceptions.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="exception">The thrown exception.</param>
    /// <returns>The failure action.</returns>
    MessageFailureAction ClassifyException(MessageContext context, Exception exception);
}
