namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record VisitedSearch : ActionBase
{
    public required string Query { get; set; }
}