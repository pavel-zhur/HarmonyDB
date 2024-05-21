using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;

public abstract class PipelineHandler
{
    private readonly BotClient _api;
    private readonly List<(string? queueKey, Func<Task> task)> _tasks = new();
    protected TelegramOptions TelegramOptions { get; }
    protected SongsDatabase SongsDatabase { get; }

    protected PipelineHandler(IOptions<TelegramOptions> telegramOptions, SongsDatabase songsDatabase)
    {
        TelegramOptions = telegramOptions.Value;
        _api = new(TelegramOptions.Token);
        SongsDatabase = songsDatabase;
    }

    public async Task<(bool handled, List<(string? queueKey, Func<Task> task)> tasks)> Handle(Update update)
    {
        var handled = await HandleSync(update);
        lock (_tasks)
        {
            return (handled, _tasks);
        }
    }

    protected abstract Task<bool> HandleSync(Update update);

    protected bool IsPrivate(Chat? chat) => chat?.Type == ChatType.Private;

    protected void QueueApi(string? queueKey, Func<BotClient, Task> task)
    {
        lock (_tasks)
        {
            _tasks.Add((queueKey == null ? null : $"{GetType().FullName}-{queueKey}", async () => await task(_api)));
        }
    }

    protected void Queued(Task task)
    {
        lock (_tasks)
        {
            _tasks.Add((null, async () => await task));
        }
    }
}