using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_tmp", "Temp")]
public class Temp : Command
{
    private readonly ILogger<Temp> _logger;
    private readonly SongsDatabase _songsDatabase;

    public Temp(ILogger<Temp> logger, Io io, SongsDatabase songsDatabase, IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
    }
}