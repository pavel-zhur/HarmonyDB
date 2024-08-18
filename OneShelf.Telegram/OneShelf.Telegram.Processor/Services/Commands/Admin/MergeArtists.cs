using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ma", "Merge artists")]
public class MergeArtists : Command
{
    private readonly ILogger<MergeArtists> _logger;
    private readonly SimpleActions _simpleActions;

    public MergeArtists(ILogger<MergeArtists> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.MergeArtists();
    }
}