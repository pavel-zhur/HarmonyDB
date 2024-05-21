using OneShelf.Common;

namespace OneShelf.Frontend.Api.Model.V3.Instant;

public record VisitedChords : ActionBase
{
    public required Uri? Uri { get; set; }

    public required int? SongId { get; set; }

    public required string? ExternalId { get; set; }

    public required string? SearchQuery { get; set; }

    public required int? Transpose { get; set; }

    public required string Artists { get; set; }

    public required string Title { get; set; }

    public required string? Source { get; set; }
}