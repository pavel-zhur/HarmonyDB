using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.V2;

public class ListResponse
{
    public required List<CollectiveVersion> Versions { get; set; }
}