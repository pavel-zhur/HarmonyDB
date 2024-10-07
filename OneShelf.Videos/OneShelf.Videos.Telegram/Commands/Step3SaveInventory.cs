using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Videos.BusinessLogic.Services;

namespace OneShelf.Videos.Telegram.Commands;

[AdminCommand("s3_saveinventory", "Реинвентаризация", "Шаг 3 - реинвентаризация всего")]
public class Step3SaveInventory(Io io, ILogger<Step3SaveInventory> logger, Service2 service2) : Command(io)
{
    protected override async Task ExecuteQuickly()
    {
        Scheduled(Run());
    }

    private async Task Run()
    {
        try
        {
            var added = await service2.SaveInventory();

            Io.WriteLine($"added: {added}");

            Io.WriteLine("Success.");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            Io.WriteLine($"{e.GetType()} {e.Message}");
        }
    }
}