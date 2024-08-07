﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.OneDog.Processor.Services;

public class ChannelActions
{
    private readonly ILogger<ChannelActions> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly TelegramOptions _telegramOptions;

    public ChannelActions(ILogger<ChannelActions> logger, IOptions<TelegramOptions> telegramOptions, DogDatabase dogDatabase)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _telegramOptions = telegramOptions.Value;
    }
    
    public async Task UpdateCommands(int domainId, List<CommandAttribute> commands)
    {
        var domain = await _dogDatabase.Domains.Include(x => x.Administrators).SingleAsync(x => x.Id == domainId);

        var api = new BotClient(domain.BotToken);

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(commands.Where(x => x.SupportsNoParameters).Select(x => new BotCommand(x.Alias, x.ButtonDescription)), new BotCommandScopeChat(_telegramOptions.AdminId)));

        foreach (var administrator in domain.Administrators.Where(a => a.Id != _telegramOptions.AdminId))
        {
            await api.SetMyCommandsAsync(new SetMyCommandsArgs(commands.Where(x => x.SupportsNoParameters).Where(x => x.Role <= Role.DomainAdmin).Select(x => new BotCommand(x.Alias, x.ButtonDescription)), new BotCommandScopeChat(administrator.Id)));
        }

        await api.SetMyCommandsAsync(new SetMyCommandsArgs(commands.Where(x => x.SupportsNoParameters).Where(x => x.Role == Role.Regular).Select(x => new BotCommand(x.Alias, x.ButtonDescription)), new BotCommandScopeDefault()));
    }
}