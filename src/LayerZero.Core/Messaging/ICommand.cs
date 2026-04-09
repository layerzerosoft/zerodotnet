namespace LayerZero.Core;

/// <summary>
/// Marks a command that completes with no response value.
/// </summary>
public interface ICommand;

/// <summary>
/// Marks a command that completes with a response value.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommand<TResponse>;
