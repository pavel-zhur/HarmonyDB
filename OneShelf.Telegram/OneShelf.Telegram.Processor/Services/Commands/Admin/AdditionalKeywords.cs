﻿using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_ak", "Additional keywords")]
public class AdditionalKeywords : Command
{
    private readonly ILogger<AdditionalKeywords> _logger;
    private readonly SimpleActions _simpleActions;

    public AdditionalKeywords(ILogger<AdditionalKeywords> logger, Io io, SimpleActions simpleActions)
        : base(io)
    {
        _logger = logger;
        _simpleActions = simpleActions;
    }

    protected override async Task ExecuteQuickly()
    {
        await _simpleActions.AdditionalKeywords();
    }
}