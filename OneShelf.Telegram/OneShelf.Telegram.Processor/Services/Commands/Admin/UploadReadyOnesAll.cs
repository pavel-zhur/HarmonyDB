using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ua", "Upload all files to telegram")]
public class UploadReadyOnesAll : Command
{
    private readonly ILogger<UploadReadyOnesAll> _logger;
    private readonly SimpleActions _simpleActions;

    public UploadReadyOnesAll(ILogger<UploadReadyOnesAll> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.UploadReadyOnes(true);
    }
}