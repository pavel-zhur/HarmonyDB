using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Base;

public abstract class Command : CommandBase
{
    protected Command(Io io, IOptions<TelegramOptions> options)
        : base(io)
    {
        Options = options.Value;
    }

    protected TelegramOptions Options { get; }
}