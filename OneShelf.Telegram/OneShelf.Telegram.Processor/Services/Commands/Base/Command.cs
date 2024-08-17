using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;

namespace OneShelf.Telegram.Processor.Services.Commands.Base;

public abstract class Command
{
    private readonly List<Task> _scheduled = new();

    protected Command(Io io, IOptions<TelegramOptions> options)
    {
        Io = io;
        Options = options.Value;
    }

    protected Io Io { get; }

    protected TelegramOptions Options { get; }

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