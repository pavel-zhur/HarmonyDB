using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Database.Songs.Services;

public class SongsDatabaseMemory
{
    private readonly ILogger<SongsDatabaseMemory> _logger;

    private volatile int _version;

    public SongsDatabaseMemory(ILogger<SongsDatabaseMemory> logger)
    {
        _logger = logger;
    }

    public void Advance()
    {
        Interlocked.Increment(ref _version);
    }

    public int Get() => _version;
}