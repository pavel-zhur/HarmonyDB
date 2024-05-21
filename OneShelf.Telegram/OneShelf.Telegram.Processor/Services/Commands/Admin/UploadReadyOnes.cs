using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_u5", "Upload 5 files to telegram")]
public class UploadReadyOnes : Command
{
    private readonly ILogger<UploadReadyOnes> _logger;
    private readonly SimpleActions _simpleActions;

    public UploadReadyOnes(ILogger<UploadReadyOnes> logger, Io io, SimpleActions simpleActions,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.UploadReadyOnes();
    }
}