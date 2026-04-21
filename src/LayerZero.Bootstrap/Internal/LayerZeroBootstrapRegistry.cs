namespace LayerZero.Bootstrap.Internal;

internal sealed class LayerZeroBootstrapRegistry
{
    private readonly List<LayerZeroBootstrapStepRegistration> steps = [];
    private readonly List<LayerZeroBootstrapCommand> commandHandlers = [];

    public IReadOnlyList<LayerZeroBootstrapStepRegistration> Steps => steps;

    public IReadOnlyList<LayerZeroBootstrapCommand> CommandHandlers => commandHandlers;

    public void AddStep(string name, LayerZeroBootstrapStep execute)
    {
        steps.Add(new LayerZeroBootstrapStepRegistration(name, execute));
    }

    public void AddCommandHandler(LayerZeroBootstrapCommand handler)
    {
        commandHandlers.Add(handler);
    }
}

internal sealed record LayerZeroBootstrapStepRegistration(
    string Name,
    LayerZeroBootstrapStep Execute);
