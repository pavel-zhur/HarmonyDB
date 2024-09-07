using System.Text.Json;
using System.Text.Json.Serialization;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.Database;
using OneShelf.Videos.Database.Models;
using OneShelf.Videos.Telegram.Processor.Services;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Videos.Telegram.Processor.PipelineHandlers;

public class UpdatesCollector : PipelineHandler
{
    private readonly VideosDatabase _videosDatabase;
    private readonly Scope _scope;

    public UpdatesCollector(IScopedAbstractions scopedAbstractions, VideosDatabase videosDatabase, Scope scope) 
        : base(scopedAbstractions)
    {
        _videosDatabase = videosDatabase;
        _scope = scope;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var dbUpdate = new TelegramUpdate
        {
            Id = update.UpdateId,
            CreatedOn = DateTime.Now,
            Json = JsonSerializer.Serialize(update, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }),
        };

        _videosDatabase.TelegramUpdates.Add(dbUpdate);
        await _videosDatabase.SaveChangesAsync();

        _scope.Initialize(dbUpdate.Id);

        return false;
    }
}