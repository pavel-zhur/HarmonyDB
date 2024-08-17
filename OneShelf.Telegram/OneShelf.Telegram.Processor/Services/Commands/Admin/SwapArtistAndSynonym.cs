using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_sws", "Swap with synonym")]
public class SwapArtistAndSynonym : Command
{
    private readonly ILogger<SwapArtistAndSynonym> _logger;
    private readonly SimpleActions _simpleActions;

    public SwapArtistAndSynonym(ILogger<SwapArtistAndSynonym> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.SwapArtistAndSynonym();
    }
}