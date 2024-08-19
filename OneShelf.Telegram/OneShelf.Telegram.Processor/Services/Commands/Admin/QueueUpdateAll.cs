using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_l", "Update All")]
public class QueueUpdateAll : Command
{
    private readonly ILogger<QueueUpdateAll> _logger;
    private readonly RegenerationQueue _regenerationQueue;

    public QueueUpdateAll(ILogger<QueueUpdateAll> logger, Io io, RegenerationQueue regenerationQueue)
        : base(io)
    {
        _logger = logger;
        _regenerationQueue = regenerationQueue;
    }

    protected override async Task ExecuteQuickly()
    {
        _regenerationQueue.QueueUpdateAll(true);
    }
}