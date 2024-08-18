using Microsoft.Extensions.Options;
using OneShelf.OneDragon.Processor.Model;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.OneDragon.Processor.Services;

public class ScopedAbstractions : IScopedAbstractions
{
    private readonly TelegramOptions _options;

    public ScopedAbstractions(IOptions<TelegramOptions> options)
    {
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
    }
}