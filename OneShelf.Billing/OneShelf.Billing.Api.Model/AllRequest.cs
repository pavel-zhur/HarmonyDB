namespace OneShelf.Billing.Api.Model;

public class AllRequest
{
    public int? DomainId { get; init; }
    
    public TimeSpan? Window { get; init; }
}