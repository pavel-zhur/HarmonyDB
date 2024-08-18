using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_llnc", "Live, None")]
public class ListLiveNoChords : Command
{
    private readonly ILogger<ListLiveNoChords> _logger;
    private readonly SimpleActions _simpleActions;

    public ListLiveNoChords(ILogger<ListLiveNoChords> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ListLiveNoChords();
    }
}