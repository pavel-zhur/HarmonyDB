using System.Text.Json;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model.Enums;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDog.Processor.Services;

public class ScopedAbstractions : IScopedAbstractions
{
    private readonly DogContext _dogContext;
    private readonly DogDatabase _dogDatabase;

    public ScopedAbstractions(DogContext dogContext, DogDatabase dogDatabase)
    {
        _dogContext = dogContext;
        _dogDatabase = dogDatabase;
    }

    public async Task Initialize(int domainId) => await _dogContext.Initialize(domainId);

    public Role GetNonAdminRole(long userId) => _dogContext.GetNonAdminRole(userId);

    public string GetBotToken() => _dogContext.GetBotToken();

    public IEnumerable<long> GetDomainAdministratorIds() => _dogContext.GetDomainAdministratorIds();

    public async Task OnDialogInteraction(Update update, long userId, string? text)
    {
        _dogDatabase.Interactions.Add(new()
        {
            InteractionType = InteractionType.Dialog,
            UserId = userId,
            CreatedOn = DateTime.Now,
            Serialized = JsonSerializer.Serialize(update),
            ShortInfoSerialized = text,
            DomainId = _dogContext.DomainId,
        });

        await _dogDatabase.SaveChangesAsync();
    }
}