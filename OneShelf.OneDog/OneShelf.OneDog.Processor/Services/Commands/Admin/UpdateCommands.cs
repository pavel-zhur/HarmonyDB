using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("uc", "Update commands", Role.Admin)]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly AvailableCommands _availableCommands;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions, AvailableCommands availableCommands, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
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
            ScopeAwareness.DomainId,
            _availableCommands
                .GetCommands(Role.Admin)
                .Select(x => x.attribute)
                .ToList());
    }
}