using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands;

[Command("nothing", "Ничего", Role.Regular, "Совсем ничего")]
public class Nothing : Command
{
    private readonly ILogger<Nothing> _logger;

    public Nothing(ILogger<Nothing> logger, Io io)
        : base(io)
    {
        _logger = logger;
    }

    protected override async Task ExecuteQuickly()
    {
        var query = Io.FreeChoice("Скажите что-нибудь:");

        Io.WriteLine("Вы сказали: ".Bold() + query);
        Io.WriteLine();
    }
}