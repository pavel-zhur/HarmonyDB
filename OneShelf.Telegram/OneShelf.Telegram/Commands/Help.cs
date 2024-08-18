using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Options;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Commands;

[Command("help", "Справка", Role.Regular, "Справка по всем командам")]
public class Help : Command
{
    private readonly ILogger<Help> _logger;
    private readonly AvailableCommands _availableCommands;
    private readonly TelegramContext _telegramContext;
    private readonly ISingletonAbstractions _singletonAbstractions;

    public Help(ILogger<Help> logger, Io io, AvailableCommands availableCommands, TelegramContext telegramContext, ISingletonAbstractions singletonAbstractions)
        : base(io)
    {
        _logger = logger;
        _availableCommands = availableCommands;
        _telegramContext = telegramContext;
        _singletonAbstractions = singletonAbstractions;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine(_singletonAbstractions.GetHelpResponseHeader());

        foreach (var command in _availableCommands.GetCommands(_telegramContext.GetRole(Io.UserId)).Where(x => x.attribute.SupportsNoParameters))
        {
            Io.WriteLine($"/{command.attribute.Alias}: {command.attribute.HelpDescription}");
        }
    }
}