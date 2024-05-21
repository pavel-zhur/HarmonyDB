using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_dl", "Drop Lists")]
public class QueueDropLists : Command
{
    private readonly ILogger<QueueDropLists> _logger;
    private readonly RegenerationQueue _regenerationQueue;

    public QueueDropLists(ILogger<QueueDropLists> logger, Io io, RegenerationQueue regenerationQueue,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _regenerationQueue = regenerationQueue;
    }

    protected override async Task ExecuteQuickly()
    {
        _regenerationQueue.QueueDropLists();
    }
}