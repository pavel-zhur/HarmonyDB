using Microsoft.EntityFrameworkCore;

namespace OneShelf.Common.Database.Songs.Model;

[Index(nameof(DomainId))]
public class BillingUsage
{
    public int Id { get; set; }

    public required string Model { get; init; }

    public required string UseCase { get; init; }

    public required int Count { get; init; }

    public long? UserId { get; init; }

    public User? User { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public required DateTime CreatedOn { get; init; }

    public string? AdditionalInfo { get; init; }

    public int? DomainId { get; init; }
}