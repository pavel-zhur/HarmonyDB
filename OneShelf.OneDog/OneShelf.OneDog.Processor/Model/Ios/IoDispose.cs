using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.OneDog.Processor.Model.Ios;

public class IoDispose : Io
{
    public IoDispose(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
        : base(userId, parameters, telegramOptions, logger)
    {
    }

    public override bool SupportsOutput => true;

    public override void Write(string line)
    {
    }
}