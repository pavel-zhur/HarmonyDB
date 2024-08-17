using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("uc", "Update commands", Role.Admin)]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly AvailableCommands _availableCommands;
    private readonly ScopeAwareness _scopeAwareness;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions, AvailableCommands availableCommands, ScopeAwareness scopeAwareness)
        : base(io)
    {
        _logger = logger;
        _channelActions = channelActions;
        _availableCommands = availableCommands;
        _scopeAwareness = scopeAwareness;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Update());
    }

    private async Task Update()
    {
        await _channelActions.UpdateCommands(
            _scopeAwareness.DomainId,
            _availableCommands
                .GetCommands(Role.Admin)
                .Select(x => x.attribute)
                .ToList());
    }
}