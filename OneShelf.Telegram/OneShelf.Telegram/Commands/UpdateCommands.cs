using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.Telegram.Commands;

[Command("uc", "Update commands", Role.Admin)]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly AvailableCommands _availableCommands;
    private readonly IScopedAbstractions _scopedAbstractions;
    private readonly TelegramOptions _options;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, AvailableCommands availableCommands, IScopedAbstractions scopedAbstractions, IOptions<TelegramOptions> options)
        : base(io)
    {
        _logger = logger;
        _availableCommands = availableCommands;
        _scopedAbstractions = scopedAbstractions;
        _options = options.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Update());
    }

    private async Task Update()
    {
        var allCommands = _availableCommands
            .GetCommands(Role.Admin)
            .Select(x => x.attribute)
            .ToList();
        
        var api = new TelegramBotClient(_scopedAbstractions.GetBotToken());

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(allCommands.Where(x => x.SupportsNoParameters).Select(x => new BotCommand(x.Alias, x.ButtonDescription)))
        {
            Scope = new BotCommandScopeChat(_options.AdminId),
        });

        foreach (var administratorId in _scopedAbstractions.GetDomainAdministratorIds())
        {
            await api.SetMyCommandsAsync(new SetMyCommandsArgs(allCommands.Where(x => x.SupportsNoParameters).Where(x => x.Role <= Role.DomainAdmin).Select(x => new BotCommand(x.Alias, x.ButtonDescription)))
            {
                Scope = new BotCommandScopeChat(administratorId),
            });
        }

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(allCommands.Where(x => x.SupportsNoParameters).Where(x => x.Role == Role.Regular).Select(x => new BotCommand(x.Alias, x.ButtonDescription)))
        {
            Scope = new BotCommandScopeDefault(),
        });
    }
}