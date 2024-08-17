using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Processor.Helpers;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;
using OneShelf.Telegram.Helpers;

namespace OneShelf.OneDog.Processor.Services.Commands;

[Command("nothing", "Ничего", Role.Regular, "Совсем ничего")]
public class Nothing : Command
{
    private readonly ILogger<Nothing> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;
    private readonly TelegramOptions _telegramOptions;

    public Nothing(ILogger<Nothing> logger, Io io, DialogHandlerMemory dialogHandlerMemory, IOptions<TelegramOptions> telegramOptions, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
        _telegramOptions = telegramOptions.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        var query = Io.FreeChoice("Скажите что-нибудь:");

        Io.WriteLine("Вы сказали: ".Bold() + query);
        Io.WriteLine();
    }
}