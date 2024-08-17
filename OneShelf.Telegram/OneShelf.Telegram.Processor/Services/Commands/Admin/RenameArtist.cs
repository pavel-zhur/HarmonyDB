using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ra", "Rename artist")]
public class RenameArtist : Command
{
    private readonly ILogger<RenameArtist> _logger;
    private readonly SimpleActions _simpleActions;

    public RenameArtist(ILogger<RenameArtist> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.RenameArtist();
    }
}