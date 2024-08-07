﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_bow", "Bowowow")]
public class Bowowow : Command
{
    private readonly ILogger<Bowowow> _logger;
    private readonly TelegramOptions _telegramOptions;
    private readonly BotClient _api;

    public Bowowow(ILogger<Bowowow> logger, Io io, IOptions<TelegramOptions> telegramOptions,
        IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _telegramOptions = telegramOptions.Value;
        _api = new(_telegramOptions.Token);
    }

    protected override async Task ExecuteQuickly()
    {
        var phrase = Io.FreeChoice("what to say?");
        Scheduled(Background(phrase));
    }

    private async Task Background(string phrase)
    {
        await _api.SendMessageAsync(_telegramOptions.PublicChatId, phrase, messageThreadId: _telegramOptions.AnnouncementsTopicId);
    }
}