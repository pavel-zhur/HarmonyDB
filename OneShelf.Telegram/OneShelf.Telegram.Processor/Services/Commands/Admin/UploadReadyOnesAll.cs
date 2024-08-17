using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ua", "Upload all files to telegram")]
public class UploadReadyOnesAll : Command
{
    private readonly ILogger<UploadReadyOnesAll> _logger;
    private readonly SimpleActions _simpleActions;

    public UploadReadyOnesAll(ILogger<UploadReadyOnesAll> logger, Io io, SimpleActions simpleActions,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.UploadReadyOnes(true);
    }
}