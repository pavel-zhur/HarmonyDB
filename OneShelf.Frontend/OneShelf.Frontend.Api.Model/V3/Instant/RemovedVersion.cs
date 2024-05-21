namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record RemovedVersion : ActionBase
{
    public required int VersionId { get; set; }
}