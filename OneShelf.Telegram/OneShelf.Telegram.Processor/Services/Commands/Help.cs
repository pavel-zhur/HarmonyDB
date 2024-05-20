using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("help", "Справка", "Справка по всем командам")]
public class Help : Command
{
    private readonly ILogger<Help> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;

    public Help(ILogger<Help> logger, Io io, DialogHandlerMemory dialogHandlerMemory, IOptions<TelegramOptions> telegramOptions)
        : base(io, telegramOptions)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Добрый день!".Bold());
        Io.WriteLine();
        Io.Write("Для быстрого поиска песни, введите часть".Bold());
        Io.WriteLine(" названия или исполнителя или номер песни.");
        Io.WriteLine();
        Io.WriteLine($"Еще вы можете упомянуть меня в любом диалоге (написать @{Options.BotUsername}), чтобы найти песню и аккорды к ней.");
        Io.WriteLine();
        Io.WriteLine("Помимо этого, вот чем я могу помочь:");
        Io.WriteLine();
        foreach (var command in _dialogHandlerMemory.GetCommands(Io.IsAdmin).Where(x => x.attribute.SupportsNoParameters))
        {
            Io.WriteLine($"/{command.attribute.Alias}: {command.attribute.HelpDescription}");
        }
    }
}