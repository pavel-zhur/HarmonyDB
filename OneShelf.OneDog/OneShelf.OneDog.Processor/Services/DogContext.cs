using Microsoft.EntityFrameworkCore;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Database.Model;
using OneShelf.Telegram.Model;

namespace OneShelf.OneDog.Processor.Services;

public class DogContext
{
    private readonly DogDatabase _dogDatabase;

    private Domain? _domain;

    public DogContext(DogDatabase dogDatabase)
    {
        _dogDatabase = dogDatabase;
    }

    public Domain Domain => _domain ?? throw new("Not initialized.");

    public int DomainId => Domain.Id;

    public async Task Initialize(int domainId)
    {
        if (_domain != null) throw new("Already initialized.");

        _domain = await _dogDatabase.Domains.Include(x => x.Administrators).SingleAsync(x => x.Id == domainId);
    }

    public Role GetRole(long userId) => _domain?.Administrators.Any(x => x.Id == userId) ?? throw new("Not initialized.")
        ? Role.DomainAdmin
        : Role.Regular;

    public string GetBotToken() => Domain.BotToken;
}