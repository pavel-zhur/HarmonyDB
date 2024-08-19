namespace OneShelf.Billing.Api.Model;

public record Usage
{
    public required string Model { get; init; }

    public required string UseCase { get; init; }

    public required int Count { get; init; }

    public long? UserId { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public DateTime? CreatedOn { get; init; }

    public string? AdditionalInfo { get; init; }

    public float? Price { get; init; }
    
    public string? Category { get; init; }

    public required int? DomainId { get; init; }
    
    public required long? ChatId { get; init; }
}