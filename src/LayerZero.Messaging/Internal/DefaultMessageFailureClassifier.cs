using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Messaging.Internal;

internal sealed class DefaultMessageFailureClassifier : IMessageFailureClassifier
{
    public MessageFailureAction ClassifyValidationFailure(MessageContext context, ValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(validation);
        return MessageFailureAction.DeadLetter;
    }

    public MessageFailureAction ClassifyResultFailure(MessageContext context, Result result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        return MessageFailureAction.DeadLetter;
    }

    public MessageFailureAction ClassifyException(MessageContext context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exception);
        return MessageFailureAction.Retry;
    }
}
