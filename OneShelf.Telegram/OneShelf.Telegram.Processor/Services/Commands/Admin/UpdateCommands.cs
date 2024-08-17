using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("uc", "Update commands")]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly AvailableCommands _availableCommands;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions,
        AvailableCommands availableCommands)
        : base(io)
    {
        _logger = logger;
        _channelActions = channelActions;
        _availableCommands = availableCommands;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Update());
    }

    private async Task Update()
    {
        await _channelActions.UpdateCommands(
            _availableCommands
                .GetCommands(Role.Regular)
                .Select(x => x.attribute)
                .ToList());
    }
}