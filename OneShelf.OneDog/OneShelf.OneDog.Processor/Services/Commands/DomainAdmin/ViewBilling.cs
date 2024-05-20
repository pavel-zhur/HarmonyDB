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
        var total = all.Usages.Where(x => x.Price.HasValue).Sum(x => x.Price);
        if (total is 0 or null)
        {
            Io.WriteLine("Пока ничего нет.");
            return;
        }

        var domain = await _dogDatabase.Domains.SingleAsync(x => x.Id == ScopeAwareness.DomainId);

        Io.WriteLine($"Использовано {total * (domain.BillingRatio ?? 1):C}.");
    }
}