using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.IoMemories;

namespace OneShelf.Telegram.Services;

public class DialogHandlerMemory
{
    private readonly ILogger<DialogHandlerMemory> _logger;
    private readonly Dictionary<long, IoMemory> _memory = new();

    public DialogHandlerMemory(ILogger<DialogHandlerMemory> logger)
    {
        _logger = logger;
    }

    public IoMemory? Get(long userId)
    {
        lock (_memory)
        {
            return _memory.TryGetValue(userId, out var value) ? value : null;
        }
    }

    public void Set(long userId, IoMemory ioMemory)
    {
        lock (_memory)
        {
            _memory[userId] = ioMemory;
        }
    }

    public void Erase(long userId)
    {
        lock (_memory)
        {
            _memory.Remove(userId);
        }
    }
}