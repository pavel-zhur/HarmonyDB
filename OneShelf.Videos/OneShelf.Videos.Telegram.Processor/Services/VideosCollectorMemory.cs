using Nito.AsyncEx;

namespace OneShelf.Videos.Telegram.Processor.Services;

public class VideosCollectorMemory
{
    public AsyncLock DatabaseLock { get; } = new();
}