using Microsoft.Extensions.Logging;

namespace OneShelf.Videos.BusinessLogic.Services.Live;

public class TelegramLoggerInitializer
{
    public TelegramLoggerInitializer(ILogger<LiveDownloader> logger)
    {
        WTelegram.Helpers.Log = (logLevel, message) => logger.Log((LogLevel)logLevel, message);
    }
}