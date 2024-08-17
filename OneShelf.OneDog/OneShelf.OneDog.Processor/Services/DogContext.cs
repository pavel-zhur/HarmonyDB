using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Options;

namespace OneShelf.OneDog.Processor.Services;

public class DogContext
{
    private readonly DogDatabase _dogDatabase;
    private readonly TelegramOptions _options;

    private Domain? _domain;

    public DogContext(DogDatabase dogDatabase, IOptions<TelegramOptions> options)
    {
        _dogDatabase = dogDatabase;
        _options = options.Value;
    }

    public Domain Domain => _domain ?? throw new("Not initialized.");

    public int DomainId => Domain.Id;

    public async Task Initialize(int domainId)
    {
        if (_domain != null) throw new("Already initialized.");

        _domain = await _dogDatabase.Domains.Include(x => x.Administrators).SingleAsync(x => x.Id == domainId);
    }

    public Role GetNonAdminRole(long userId)
        => _domain?.Administrators.Any(x => x.Id == userId) ?? throw new("Not initialized.")
            ? Role.DomainAdmin
            : Role.Regular;

    public string GetBotToken() => Domain.BotToken;

    public IEnumerable<long> GetDomainAdministratorIds() => Domain.Administrators.Where(a => a.Id != _options.AdminId).Select(x => x.Id);
}