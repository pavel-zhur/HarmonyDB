using OneShelf.Telegram.Model;

namespace OneShelf.Telegram.Services.Base;

public interface IScopedAbstractions
{
    Task Initialize(int domainId);

    Role GetNonAdminRole(long userId);

    string GetBotToken();
    
    IEnumerable<long> GetDomainAdministratorIds();
}