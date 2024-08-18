using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_lar", "Archived?")]
public class ListArchived : Command
{
    private readonly ILogger<ListArchived> _logger;
    private readonly SimpleActions _simpleActions;

    public ListArchived(ILogger<ListArchived> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ListArchived();
    }
}