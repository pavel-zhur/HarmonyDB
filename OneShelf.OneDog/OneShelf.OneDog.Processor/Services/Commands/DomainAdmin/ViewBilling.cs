using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Billing.Api.Client;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;

namespace OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;

[Command("a_billing", "Биллинг", Role.DomainAdmin, "Отчёт")]
public class ViewBilling : Command
{
    private readonly ILogger<ViewBilling> _logger;
    private readonly DogDatabase _dogDatabase;
    private readonly BillingApiClient _billingApiClient;

    public ViewBilling(ILogger<ViewBilling> logger, Io io, DogDatabase dogDatabase, BillingApiClient billingApiClient, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dogDatabase = dogDatabase;
        _billingApiClient = billingApiClient;
    }

    protected override async Task ExecuteQuickly()
    {
        Scheduled(ExecuteBackground());
    }

    private async Task ExecuteBackground()
    {
        var all = await _billingApiClient.All(ScopeAwareness.DomainId);
        var totals = all.Usages
            .Where(x => x.Price > 0)
            .GroupBy(x => x.Category ?? "unknown")
            .ToDictionary(x => x.Key, x => x.Sum(x => x.Price!.Value));

        if (!totals.Any())
        {
            Io.WriteLine("Пока ничего нет.");
            return;
        }

        var domain = await _dogDatabase.Domains.SingleAsync(x => x.Id == ScopeAwareness.DomainId);

        foreach (var total in totals)
        {
            Io.WriteLine($"Использовано {total.Key} {total.Value * (domain.BillingRatio ?? 1):C}.");
        }
    }
}