using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ld", "Drafts?")]
public class ListDraft : Command
{
    private readonly ILogger<ListDraft> _logger;
    private readonly SimpleActions _simpleActions;

    public ListDraft(ILogger<ListDraft> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ListDraft();
    }
}