using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services;

public class ScopedAbstractions : IScopedAbstractions
{
    private readonly DogContext _dogContext;
    
    public ScopedAbstractions(DogContext dogContext)
    {
        _dogContext = dogContext;
    }

    public async Task Initialize(int domainId) => await _dogContext.Initialize(domainId);

    public Role GetNonAdminRole(long userId) => _dogContext.GetRole(userId);

    public string GetBotToken() => _dogContext.GetBotToken();
}