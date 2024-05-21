namespace OneShelf.Billing.Api.Model;

public class AllResponse
{
    public required List<Usage> Usages { get; set; }

    public required Dictionary<long, string> UserTitles { get; set; }
}