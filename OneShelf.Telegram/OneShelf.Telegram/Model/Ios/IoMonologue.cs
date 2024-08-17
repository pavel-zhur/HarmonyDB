using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Options;

namespace OneShelf.Telegram.Model.Ios;

public class IoMonologue : IoWithFinishBase
{
    public IoMonologue(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger) 
        : base(userId, parameters, telegramOptions, logger)
    {
    }

    public override bool SupportsMarkdownOutput => true;
}