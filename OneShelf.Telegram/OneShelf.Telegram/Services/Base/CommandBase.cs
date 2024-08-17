using OneShelf.Telegram.Model.Ios;

namespace OneShelf.Telegram.Services.Base;

public abstract class CommandBase
{
    private readonly List<Task> _scheduled = new();

    protected CommandBase(Io io)
    {
        Io = io;
    }

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