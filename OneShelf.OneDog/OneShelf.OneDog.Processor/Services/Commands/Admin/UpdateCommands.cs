using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Processor.Model;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("uc", "Update commands", Role.Admin)]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly DialogHandlerMemory _dialogHandlerMemory;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions, DialogHandlerMemory dialogHandlerMemory, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
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
            ScopeAwareness.DomainId,
            _dialogHandlerMemory
                .GetCommands(Role.Admin)
                .Select(x => x.attribute)
                .ToList());
    }
}