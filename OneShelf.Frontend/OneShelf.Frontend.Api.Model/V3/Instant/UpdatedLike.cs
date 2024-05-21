namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record UpdatedLike : ActionBase
{
    public required int SongId { get; set; }
    public required int? VersionId { get; set; }
    public required byte? Level { get; set; }
    public required int? Transpose { get; set; }
    public required int? LikeCategoryId { get; set; }
}