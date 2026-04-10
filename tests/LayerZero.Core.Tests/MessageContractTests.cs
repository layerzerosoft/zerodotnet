namespace LayerZero.Core.Tests;

public sealed class MessageContractTests
{
    [Fact]
    public async Task Command_handler_returns_result()
    {
        var handler = new CreateNoteHandler();

        var result = await handler.HandleAsync(
            new CreateNote("Slice mechanics"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Slice mechanics", result.Value.Title);
    }

    [Fact]
    public async Task Event_handler_returns_result()
    {
        var handler = new NoteCreatedHandler();

        var result = await handler.HandleAsync(
            new NoteCreated(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    private sealed record CreateNote(string Title) : ICommand<CreateNoteResponse>;

    private sealed record CreateNoteResponse(string Title);

    private sealed class CreateNoteHandler : ICommandHandler<CreateNote, CreateNoteResponse>
    {
        public ValueTask<Result<CreateNoteResponse>> HandleAsync(
            CreateNote command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result<CreateNoteResponse>.Success(new CreateNoteResponse(command.Title)));
        }
    }

    private sealed record NoteCreated(Guid Id) : IEvent;

    private sealed class NoteCreatedHandler : IEventHandler<NoteCreated>
    {
        public ValueTask<Result> HandleAsync(NoteCreated message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result.Success());
        }
    }
}
