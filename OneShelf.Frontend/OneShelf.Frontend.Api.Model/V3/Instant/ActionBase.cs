namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record ActionBase
{
    public Guid ActionId { get; set; } = Guid.NewGuid();

    public required DateTime HappenedOn { get; set; }
}