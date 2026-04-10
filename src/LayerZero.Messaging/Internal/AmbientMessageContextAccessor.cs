namespace LayerZero.Messaging.Internal;

internal sealed class AmbientMessageContextAccessor : IMessageContextAccessor
{
    private static readonly AsyncLocal<MessageContext?> CurrentContext = new();

    public MessageContext? Current => CurrentContext.Value;

    public IDisposable Push(MessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope(MessageContext? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentContext.Value = previous;
        }
    }
}
