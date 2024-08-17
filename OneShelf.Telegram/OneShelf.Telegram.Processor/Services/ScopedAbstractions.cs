using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services;

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
}