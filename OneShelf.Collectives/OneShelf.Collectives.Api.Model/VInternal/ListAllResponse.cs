using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.VInternal;

public class ListAllResponse
{
    public required List<CollectiveVersion> Versions { get; set; }
}