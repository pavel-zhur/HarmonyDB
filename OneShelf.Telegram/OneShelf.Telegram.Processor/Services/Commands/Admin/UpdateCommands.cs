using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("uc", "Update commands")]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly DialogHandlerMemory _dialogHandlerMemory;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions,
        DialogHandlerMemory dialogHandlerMemory, IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _channelActions = channelActions;
        _dialogHandlerMemory = dialogHandlerMemory;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Update());
    }

    private async Task Update()
    {
        await _channelActions.UpdateCommands(
            _dialogHandlerMemory
                .GetCommands(null)
                .Select(x => x.attribute)
                .ToList());
    }
}