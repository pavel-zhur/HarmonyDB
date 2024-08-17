using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Processor.Helpers;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;
using OneShelf.Telegram.Helpers;

namespace OneShelf.OneDog.Processor.Services.Commands;

[Command("start", "В начало", Role.Regular)]
public class Start : Command
{
    private readonly ILogger<Start> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;

    public Start(ILogger<Start> logger, Io io, DialogHandlerMemory dialogHandlerMemory, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Добрый день!".Bold());
        Io.WriteLine();
        Io.WriteLine("Моя польза только в том чате, в котором я.");
        Io.WriteLine("Тут ничего особого нет пока, можете настроить мою личность если вы администратор, можете посмотреть помощь - /help.");
    }
}