using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("help", "Справка", "Справка по всем командам")]
public class Help : Command
{
    private readonly ILogger<Help> _logger;
    private readonly AvailableCommands _availableCommands;
    private readonly TelegramOptions _options;

    public Help(ILogger<Help> logger, Io io, AvailableCommands availableCommands, IOptions<TelegramOptions> options)
        : base(io)
    {
        _logger = logger;
        _availableCommands = availableCommands;
        _options = options.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Добрый день!".Bold());
        Io.WriteLine();
        Io.Write("Для быстрого поиска песни, введите часть".Bold());
        Io.WriteLine(" названия или исполнителя или номер песни.");
        Io.WriteLine();
        Io.WriteLine($"Еще вы можете упомянуть меня в любом диалоге (написать @{_options.BotUsername}), чтобы найти песню и аккорды к ней.");
        Io.WriteLine();
        Io.WriteLine("Помимо этого, вот чем я могу помочь:");
        Io.WriteLine();
        foreach (var command in _availableCommands.GetCommands(Io.IsAdmin ? Role.Admin : Role.Regular).Where(x => x.attribute.SupportsNoParameters))
        {
            Io.WriteLine($"/{command.attribute.Alias}: {command.attribute.HelpDescription}");
        }
    }
}