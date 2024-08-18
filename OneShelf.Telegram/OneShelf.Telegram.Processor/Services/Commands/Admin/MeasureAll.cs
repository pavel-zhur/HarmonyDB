using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_m", "Measure All")]
public class MeasureAll : Command
{
    private readonly ILogger<MeasureAll> _logger;
    private readonly SimpleActions _simpleActions;

    public MeasureAll(ILogger<MeasureAll> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.MeasureAll();
    }
}