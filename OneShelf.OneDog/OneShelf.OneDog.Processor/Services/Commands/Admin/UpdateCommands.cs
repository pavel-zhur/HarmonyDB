using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("uc", "Update commands", Role.Admin)]
public class UpdateCommands : Command
{
    private readonly ILogger<UpdateCommands> _logger;
    private readonly ChannelActions _channelActions;
    private readonly AvailableCommands _availableCommands;
    private readonly TelegramContext _telegramContext;

    public UpdateCommands(ILogger<UpdateCommands> logger, Io io, ChannelActions channelActions, AvailableCommands availableCommands, TelegramContext telegramContext)
        : base(io)
    {
        _logger = logger;
        _channelActions = channelActions;
        _availableCommands = availableCommands;
        _telegramContext = telegramContext;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(Update());
    }

    private async Task Update()
    {
        await _channelActions.UpdateCommands(
            _telegramContext.DomainId,
            _availableCommands
                .GetCommands(Role.Admin)
                .Select(x => x.attribute)
                .ToList());
    }
}