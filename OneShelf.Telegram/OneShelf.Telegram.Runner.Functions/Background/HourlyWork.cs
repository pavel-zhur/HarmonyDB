using OneShelf.Common.Api.Background;
using OneShelf.Telegram.Processor.Services;

namespace OneShelf.Telegram.Runner.Functions.Background;

/// <summary>
/// Queues the rebuild of the library in the chat.
/// </summary>
public class HourlyWork : IScheduledWork
{
    private readonly RegenerationQueue _regenerationQueue;

    public HourlyWork(RegenerationQueue regenerationQueue)
    {
        _regenerationQueue = regenerationQueue;
    }

    public Task Run(CancellationToken cancellationToken)
    {
        _regenerationQueue.QueueUpdateAll(false);
        return Task.CompletedTask;
    }
}
