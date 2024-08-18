using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_dl", "Drop Lists")]
public class QueueDropLists : Command
{
    private readonly ILogger<QueueDropLists> _logger;
    private readonly RegenerationQueue _regenerationQueue;

    public QueueDropLists(ILogger<QueueDropLists> logger, Io io, RegenerationQueue regenerationQueue)
        : base(io)
    {
        _logger = logger;
        _regenerationQueue = regenerationQueue;
    }

    protected override async Task ExecuteQuickly()
    {
        _regenerationQueue.QueueDropLists();
    }
}