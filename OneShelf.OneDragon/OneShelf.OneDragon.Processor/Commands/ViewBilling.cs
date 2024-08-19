using Microsoft.Extensions.Logging;
using OneShelf.Billing.Api.Client;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.Commands;

[BothCommand("usage", "Использование", "Отчёт")]
public class ViewBilling : Command
{
    private readonly ILogger<ViewBilling> _logger;
    private readonly BillingApiClient _billingApiClient;
    private readonly TelegramContext _telegramContext;

    public ViewBilling(ILogger<ViewBilling> logger, Io io, BillingApiClient billingApiClient, TelegramContext telegramContext)
        : base(io)
    {
        _logger = logger;
        _billingApiClient = billingApiClient;
        _telegramContext = telegramContext;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Скоро скажу...");
        Scheduled(ExecuteBackground());
    }

    private async Task ExecuteBackground()
    {
        var all = await _billingApiClient.All(_telegramContext.DomainId, Io.UserId);
        var totals = all.Usages
            .Where(x => x.Price > 0)
            .GroupBy(x => x.Category ?? "unknown")
            .ToDictionary(x => x.Key, x => x.Sum(x => x.Price!.Value));

        if (!totals.Any())
        {
            Io.WriteLine("Пока ничего нет.");
            return;
        }

        foreach (var total in totals)
        {
            Io.WriteLine($"Использовано {total.Key} {total.Value:C}.");
        }
    }
}