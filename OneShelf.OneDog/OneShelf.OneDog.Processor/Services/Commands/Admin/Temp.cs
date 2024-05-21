using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.Admin;

[Command("a_tmp", "Temp", Role.Admin)]
public class Temp : Command
{
    private readonly ILogger<Temp> _logger;
    private readonly DogDatabase _dogDatabase;

    public Temp(ILogger<Temp> logger, Io io, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
    }

    protected override async Task ExecuteQuickly()
    {
    }
}