using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Commands;

[Command("start", "В начало", Role.Regular)]
public class Start : Command
{
    private readonly ILogger<Start> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;
    private readonly ISingletonAbstractions _singletonAbstractions;

    public Start(ILogger<Start> logger, Io io, DialogHandlerMemory dialogHandlerMemory, ISingletonAbstractions singletonAbstractions)
        : base(io)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
        _singletonAbstractions = singletonAbstractions;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.Write(_singletonAbstractions.GetStartResponse());
    }
}