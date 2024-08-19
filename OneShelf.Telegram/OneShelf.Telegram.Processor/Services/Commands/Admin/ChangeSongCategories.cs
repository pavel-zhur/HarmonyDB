using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_sc", "Song Category")]
public class ChangeSongCategories : Command
{
    private readonly ILogger<ChangeSongCategories> _logger;
    private readonly SimpleActions _simpleActions;

    public ChangeSongCategories(ILogger<ChangeSongCategories> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ChangeSongCategories();
    }
}