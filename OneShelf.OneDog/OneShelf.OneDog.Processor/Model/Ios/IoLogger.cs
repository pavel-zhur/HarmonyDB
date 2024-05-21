using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OneShelf.OneDog.Processor.Model.Ios;

public class IoLogger : Io
{
    public IoLogger(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
        : base(userId, parameters, telegramOptions, logger)
    {
    }

    public override bool SupportsOutput => true;

    public override void Write(string line) => Logger.LogInformation(line);

    protected override void CheckAnyOk()
    {
    }
}