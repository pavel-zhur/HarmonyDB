using OneShelf.Telegram.Model;
using Telegram.BotAPI.GettingUpdates;

namespace OneShelf.Telegram.Services.Base;

public interface IScopedAbstractions
{
    Task Initialize(int domainId);

    Role GetNonAdminRole(long userId);

    string GetBotToken();
    
    IEnumerable<long> GetDomainAdministratorIds();

    Task OnDialogInteraction(Update update, long userId, string? text);
}