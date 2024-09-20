using Microsoft.Extensions.Logging;

namespace OneShelf.Videos.BusinessLogic.Services;

public class TelegramLoggerInitializer
{
    public TelegramLoggerInitializer(ILogger<Service4> logger)
    {
        WTelegram.Helpers.Log = (logLevel, message) => logger.Log((LogLevel)logLevel, message);
    }
}