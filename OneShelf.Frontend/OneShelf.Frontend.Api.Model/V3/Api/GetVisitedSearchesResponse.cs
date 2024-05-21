using OneShelf.Frontend.Api.Model.V3.Instant;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetVisitedSearchesResponse
{
    public required List<VisitedSearch> VisitedSearches { get; init; }

    public required int PagesCount { get; init; }
}