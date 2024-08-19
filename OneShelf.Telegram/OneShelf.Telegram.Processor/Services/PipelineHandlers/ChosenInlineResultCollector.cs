using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class ChosenInlineResultCollector : PipelineHandler
{
    private readonly ILogger<ChosenInlineResultCollector> _logger;
    private readonly SongsDatabase _songsDatabase;

    public ChosenInlineResultCollector(ILogger<ChosenInlineResultCollector> logger, SongsDatabase songsDatabase, IScopedAbstractions scopedAbstractions)
        : base(scopedAbstractions)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.ChosenInlineResult == null) return false;

        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            UserId = update.ChosenInlineResult.From.Id,
            InteractionType = InteractionType.ChosenInlineResult,
            Serialized = JsonSerializer.Serialize(update),
            ShortInfoSerialized = $"index: {update.ChosenInlineResult.ResultId}; {update.ChosenInlineResult.Query}",
        });
        await _songsDatabase.SaveChangesAsyncX();

        return true;
    }
}