using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.IoMemories;

namespace OneShelf.OneDog.Processor.Services;

public class IoFactory
{
    private readonly IOptions<TelegramOptions> _telegramOptions;

    private Io? _io;
    private readonly ILogger<Io> _ioLogger;
    private readonly ILogger<IoFactory> _logger;

    public IoFactory(IOptions<TelegramOptions> telegramOptions, ILogger<Io> ioLogger, ILogger<IoFactory> logger)
    {
        _telegramOptions = telegramOptions;
        _ioLogger = ioLogger;
        _logger = logger;
    }

    public void InitDialog(IoMemory ioMemory)
    {
        CheckNotInitialized();
        _io = new IoDialogue(ioMemory, _telegramOptions, _ioLogger);
    }

    public void InitDialog(long userId, string alias, string? parameters)
    {
        CheckNotInitialized();
        _io = new IoDialogue(userId, alias, parameters, _telegramOptions, _ioLogger);
    }

    public void InitMonologue(long userId, string? parameters)
    {
        CheckNotInitialized();
        _io = new IoMonologue(userId, parameters, _telegramOptions, _ioLogger);
    }

    public void InitLogger(long userId, string? parameters)
    {
        CheckNotInitialized();
        _io = new IoLogger(userId, parameters, _telegramOptions, _ioLogger);
    }

    public void InitDispose(long userId, string? parameters)
    {
        CheckNotInitialized();
        _io = new IoLogger(userId, parameters, _telegramOptions, _ioLogger);
    }

    public void InitSilence(long userId, string? parameters)
    {
        CheckNotInitialized();
        _io = new IoSilence(userId, parameters, _telegramOptions, _ioLogger);
    }

    public Io Io => _io ?? throw new("Not initialized.");

    private void CheckNotInitialized()
    {
        if (_io != null) throw new("Double initialization.");
    }
}