using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("start", "В начало")]
public class Start : Command
{
    private readonly ILogger<Start> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;

    public Start(ILogger<Start> logger, Io io, DialogHandlerMemory dialogHandlerMemory,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Добрый день!".Bold());
        Io.WriteLine();
        Io.Write("Для быстрого поиска песни,".Bold());
        Io.WriteLine(" введите номер или часть названия или исполнителя.");
        Io.WriteLine();
        Io.WriteLine("Или выберите следующую команду, или посмотрите помощь - /help.");
    }
}