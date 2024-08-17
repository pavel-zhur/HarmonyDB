using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model;
using OneShelf.Telegram.Model;

namespace OneShelf.OneDog.Processor.Services;

public class TelegramContext
{
    private readonly DogDatabase _dogDatabase;
    private readonly TelegramOptions _options;

    private Domain? _domain;

    public TelegramContext(DogDatabase dogDatabase, IOptions<TelegramOptions> options)
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

    public Role GetRole(long userId)
    {
        return userId == _options.AdminId
            ? Role.Admin
            : _domain == null
                ? throw new("Not initialized.")
                : _domain.Administrators.Any(x => x.Id == userId)
                    ? Role.DomainAdmin
                    : Role.Regular;
    }
}