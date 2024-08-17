using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class ChosenInlineResultCollector : PipelineHandler
{
    private readonly ILogger<ChosenInlineResultCollector> _logger;

    public ChosenInlineResultCollector(IOptions<TelegramOptions> telegramOptions, ILogger<ChosenInlineResultCollector> logger, SongsDatabase songsDatabase)
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.ChosenInlineResult == null) return false;

        SongsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            UserId = update.ChosenInlineResult.From.Id,
            InteractionType = InteractionType.ChosenInlineResult,
            Serialized = JsonSerializer.Serialize(update),
            ShortInfoSerialized = $"index: {update.ChosenInlineResult.ResultId}; {update.ChosenInlineResult.Query}",
        });
        await SongsDatabase.SaveChangesAsyncX();

        return true;
    }
}