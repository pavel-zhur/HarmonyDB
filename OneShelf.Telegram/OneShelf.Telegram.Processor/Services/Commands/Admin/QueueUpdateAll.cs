using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_l", "Update All")]
public class QueueUpdateAll : Command
{
    private readonly ILogger<QueueUpdateAll> _logger;
    private readonly RegenerationQueue _regenerationQueue;

    public QueueUpdateAll(ILogger<QueueUpdateAll> logger, Io io, RegenerationQueue regenerationQueue,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _regenerationQueue = regenerationQueue;
    }

    protected override async Task ExecuteQuickly()
    {
        _regenerationQueue.QueueUpdateAll(true);
    }
}