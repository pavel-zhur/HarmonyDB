using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.Commands;

[BothCommand("default", "Default", SupportsNoParameters = false, SupportsParameters = true)]
public class Default : Command
{
    public Default(Io io) : base(io)
    {
    }

    protected override async Task ExecuteQuickly()
    {
        var response = Io.FreeChoice("request");
        Io.WriteLine($"You said: '{response}'.");
    }
}