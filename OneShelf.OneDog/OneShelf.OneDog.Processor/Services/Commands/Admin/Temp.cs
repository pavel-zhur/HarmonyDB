using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("a_tmp", "Temp", Role.Admin)]
public class Temp : Command
{
    private readonly ILogger<Temp> _logger;

    public Temp(ILogger<Temp> logger, Io io)
        : base(io)
    {
        _logger = logger;
    }

    protected override async Task ExecuteQuickly()
    {
    }
}