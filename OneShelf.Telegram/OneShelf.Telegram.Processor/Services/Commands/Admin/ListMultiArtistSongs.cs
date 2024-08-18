using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_lmas", "List multi-artist")]
public class ListMultiArtistSongs : Command
{
    private readonly ILogger<ListMultiArtistSongs> _logger;
    private readonly SimpleActions _simpleActions;

    public ListMultiArtistSongs(ILogger<ListMultiArtistSongs> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ListMultiArtistSongs();
    }
}