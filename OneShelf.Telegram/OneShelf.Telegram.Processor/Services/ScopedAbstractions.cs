using System.Text.Json;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services;

public class ScopedAbstractions : IScopedAbstractions
{
    private readonly TelegramOptions _options;
    private readonly SongsDatabase _songsDatabase;

    public ScopedAbstractions(IOptions<TelegramOptions> options, SongsDatabase songsDatabase)
    {
        _songsDatabase = songsDatabase;
        _options = options.Value;
    }

    public async Task Initialize(int domainId)
    {
    }

    public Role GetNonAdminRole(long userId) => Role.Regular;

    public string GetBotToken() => _options.Token;

    public IEnumerable<long> GetDomainAdministratorIds() => [];

    public async Task OnDialogInteraction(Update update, long userId, string? text)
    {
        _songsDatabase.Interactions.Add(new()
        {
            InteractionType = InteractionType.Dialog,
            UserId = userId,
            CreatedOn = DateTime.Now,
            Serialized = JsonSerializer.Serialize(update),
            ShortInfoSerialized = text,
        });

        await _songsDatabase.SaveChangesAsyncX();
    }
}