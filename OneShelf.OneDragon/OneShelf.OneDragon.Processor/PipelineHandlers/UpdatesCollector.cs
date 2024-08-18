using System.Text.Json;
using System.Text.Json.Serialization;
using OneShelf.OneDragon.Database;
using OneShelf.OneDragon.Processor.Services;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDragon.Processor.PipelineHandlers;

public class UpdatesCollector : PipelineHandler
{
    private readonly DragonDatabase _dragonDatabase;
    private readonly DragonScope _scope;

    public UpdatesCollector(IScopedAbstractions scopedAbstractions, DragonDatabase dragonDatabase, DragonScope scope) 
        : base(scopedAbstractions)
    {
        _dragonDatabase = dragonDatabase;
        _scope = scope;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        var dbUpdate = new Database.Model.Update
        {
            CreatedOn = DateTime.Now,
            Json = JsonSerializer.Serialize(update, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }),
        };

        _dragonDatabase.Updates.Add(dbUpdate);
        await _dragonDatabase.SaveChangesAsync();

        _scope.Initialize(dbUpdate.Id);
        return false;
    }
}