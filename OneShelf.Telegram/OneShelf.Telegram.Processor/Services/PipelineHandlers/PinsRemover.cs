using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class PinsRemover : PipelineHandler
{
    private readonly ILogger<PinsRemover> _logger;

    public PinsRemover(ILogger<PinsRemover> logger, IOptions<TelegramOptions> telegramOptions, SongsDatabase songsDatabase)
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.Message?.From?.Id != TelegramOptions.BotId) return false;

        var chatFound =
            update.Message.MessageThreadId == TelegramOptions.PublicTopicId || update.Message.MessageThreadId == TelegramOptions.AnnouncementsTopicId
            && TelegramOptions.PublicChatId.Substring(1) == update.Message.Chat.Username;

        if (!chatFound) return false;

        QueueApi(null, async api => await api.DeleteMessageAsync(TelegramOptions.PublicChatId, update.Message.MessageId));

        _logger.LogInformation("Pin removal queued.");

        return true;
    }
}