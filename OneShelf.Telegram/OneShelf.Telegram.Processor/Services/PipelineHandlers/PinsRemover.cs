using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class PinsRemover : PipelineHandler
{
    private readonly ILogger<PinsRemover> _logger;
    private readonly TelegramOptions _telegramOptions;

    public PinsRemover(ILogger<PinsRemover> logger, IOptions<TelegramOptions> telegramOptions, IScopedAbstractions scopedAbstractions)
        : base(scopedAbstractions)
    {
        _logger = logger;
        _telegramOptions = telegramOptions.Value;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.Message?.From?.Id != _telegramOptions.BotId) return false;

        var chatFound =
            update.Message.MessageThreadId == _telegramOptions.PublicTopicId || update.Message.MessageThreadId == _telegramOptions.AnnouncementsTopicId
            && _telegramOptions.PublicLibraryChatId.Substring(1) == update.Message.Chat.Username;

        if (!chatFound) return false;

        QueueApi(null, async api => await api.DeleteMessageAsync(_telegramOptions.PublicLibraryChatId, update.Message.MessageId));

        _logger.LogInformation("Pin removal queued.");

        return true;
    }
}