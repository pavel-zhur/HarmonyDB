namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record ImportedVersion : ActionBase
{
    public required string ExternalId { get; set; }
    public required byte Level { get; set; }
    public required int Transpose { get; set; }
    public int? LikeCategoryId { get; set; }
}