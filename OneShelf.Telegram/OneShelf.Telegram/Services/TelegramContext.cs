using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Services;

public class TelegramContext
{
    private readonly IScopedAbstractions _scopedAbstractions;
    private readonly TelegramOptions _options;

    private int? _domainId;

    public TelegramContext(IOptions<TelegramOptions> options, IScopedAbstractions scopedAbstractions)
    {
        _scopedAbstractions = scopedAbstractions;
        _options = options.Value;
    }

    public int DomainId => _domainId ?? throw new("Not initialized.");

    public void Initialize(int domainId)
    {
        if (_domainId != null) throw new("Already initialized.");

        _domainId = domainId;
    }

    public Role GetRole(long userId)
    {
        return userId == _options.AdminId
            ? Role.Admin
            : _domainId == null
                ? throw new("Not initialized.")
                : _scopedAbstractions.GetNonAdminRole(userId);
    }
}