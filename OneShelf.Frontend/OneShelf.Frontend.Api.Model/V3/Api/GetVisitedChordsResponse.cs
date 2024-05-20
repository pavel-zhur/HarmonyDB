using OneShelf.Frontend.Api.Model.V3.Instant;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetVisitedChordsResponse
{
    public required List<VisitedChords> VisitedChords { get; init; }

    public required int PagesCount { get; init; }
}