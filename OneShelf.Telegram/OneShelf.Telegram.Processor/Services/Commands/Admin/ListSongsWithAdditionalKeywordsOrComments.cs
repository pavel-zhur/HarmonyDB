﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_laks", "Live with keywords/comments")]
public class ListSongsWithAdditionalKeywordsOrComments : Command
{
    private readonly ILogger<ListSongsWithAdditionalKeywordsOrComments> _logger;
    private readonly SimpleActions _simpleActions;

    public ListSongsWithAdditionalKeywordsOrComments(ILogger<ListSongsWithAdditionalKeywordsOrComments> logger, Io io,
        SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.ListSongsWithAdditionalKeywordsOrComments();
    }
}