using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ac", "Artist category")]
public class ChangeArtistCategories : Command
{
    private readonly ILogger<ChangeArtistCategories> _logger;
    private readonly SimpleActions _simpleActions;

    public ChangeArtistCategories(ILogger<ChangeArtistCategories> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ChangeArtistCategories();
    }
}