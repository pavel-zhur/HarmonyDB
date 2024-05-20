using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class PublicChatterHandler : ChatterHandlerBase
{
    private readonly ILogger<PublicChatterHandler> _logger;

    public PublicChatterHandler(
        ILogger<PublicChatterHandler> logger,
        IOptions<TelegramOptions> telegramOptions,
        SongsDatabase songsDatabase)
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (!CheckTopicId(update, TelegramOptions.PublicChatterTopicId)) return false;

        await Log(update, InteractionType.PublicChatterMessage);

        return false;
    }
}