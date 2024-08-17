using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.Telegram.Model.Ios;

public class IoSilence : Io
{
    public IoSilence(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
        : base(userId, parameters, telegramOptions, logger)
    {
    }

    protected override void CheckAnyOk()
    {
        throw new InvalidOperationException("Nothing is supported.");
    }
}