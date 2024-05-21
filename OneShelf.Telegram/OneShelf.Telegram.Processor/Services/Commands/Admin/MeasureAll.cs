using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_m", "Measure All")]
public class MeasureAll : Command
{
    private readonly ILogger<MeasureAll> _logger;
    private readonly SimpleActions _simpleActions;

    public MeasureAll(ILogger<MeasureAll> logger, Io io, SimpleActions simpleActions, IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.MeasureAll();
    }
}