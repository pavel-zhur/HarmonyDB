using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.VInternal;

public class GetManyResponse
{
    /// <summary>
    /// The collectives asked that exist and are not deleted; the others are left out.
    /// </summary>
    public required List<CollectiveVersion> Versions { get; set; }
}
