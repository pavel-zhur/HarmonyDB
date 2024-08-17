using Microsoft.Extensions.Logging;

namespace OneShelf.Telegram.Services;

public class PipelineMemory
{
    private readonly ILogger<PipelineMemory> _logger;
    private readonly Dictionary<string, SemaphoreSlim> _semaphores = new();

    public PipelineMemory(ILogger<PipelineMemory> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteInOrder(string? queueKey, Func<Task> task)
    {
        if (queueKey == null)
        {
            await task();
            return;
        }

        SemaphoreSlim semaphore;
        lock (_semaphores)
        {
            semaphore = _semaphores.TryGetValue(queueKey, out var value) ? value : _semaphores[queueKey] = new(1);
        }

        await semaphore.WaitAsync();
        try
        {
            await task();
        }
        finally
        {
            semaphore.Release();
        }
    }
}