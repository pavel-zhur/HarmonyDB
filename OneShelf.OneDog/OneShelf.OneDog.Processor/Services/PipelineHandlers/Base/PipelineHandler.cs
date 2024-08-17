using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services.PipelineHandlers.Base;

public abstract class PipelineHandler
{
    private readonly List<(string? queueKey, Func<Task> task)> _tasks = new();
    protected TelegramOptions TelegramOptions { get; }
    protected DogDatabase DogDatabase { get; }
    protected ScopeAwareness ScopeAwareness { get; }

    protected PipelineHandler(IOptions<TelegramOptions> telegramOptions, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
    {
        TelegramOptions = telegramOptions.Value;
        DogDatabase = dogDatabase;
        ScopeAwareness = scopeAwareness;
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

    protected bool IsPrivate(Chat? chat) => chat?.Type == ChatTypes.Private;

    protected void QueueApi(string? queueKey, Func<TelegramBotClient, Task> task)
    {
        lock (_tasks)
        {
            _tasks.Add((queueKey == null ? null : $"{GetType().FullName}-{queueKey}", async () => await task(new(ScopeAwareness.Domain.BotToken))));
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