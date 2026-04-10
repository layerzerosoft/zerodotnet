using LayerZero.Core;

namespace LayerZero.Messaging;

/// <summary>
/// Sends LayerZero commands over a configured transport.
/// </summary>
public interface ICommandSender
{
    /// <summary>
    /// Sends a command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    ValueTask<Result> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand;
}
