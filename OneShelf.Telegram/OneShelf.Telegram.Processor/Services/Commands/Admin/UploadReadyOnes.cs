using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_u5", "Upload 5 files to telegram")]
public class UploadReadyOnes : Command
{
    private readonly ILogger<UploadReadyOnes> _logger;
    private readonly SimpleActions _simpleActions;

    public UploadReadyOnes(ILogger<UploadReadyOnes> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.UploadReadyOnes();
    }
}