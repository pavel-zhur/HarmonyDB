using OneShelf.Telegram.Model.Ios;

namespace OneShelf.OneDog.Processor.Services.Commands.Base;

public abstract class Command
{
    private readonly List<Task> _scheduled = new();

    protected Command(Io io, ScopeAwareness scopeAwareness)
    {
        ScopeAwareness = scopeAwareness;
        Io = io;
    }

    protected ScopeAwareness ScopeAwareness { get; }

    protected Io Io { get; }

    public async Task Execute()
    {
        await ExecuteQuickly();
    }

    public Task? GetAnyScheduled() => _scheduled.Any() ? Task.WhenAll(_scheduled) : null;

    protected abstract Task ExecuteQuickly();

    protected void Scheduled(Task task)
    {
        _scheduled.Add(task);
    }
}