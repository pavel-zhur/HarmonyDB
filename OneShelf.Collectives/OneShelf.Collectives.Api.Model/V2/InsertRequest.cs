using OneShelf.Authorization.Api.Model;
using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.V2;

public class InsertRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required Collective Collective { get; set; }

    public required int? DerivedFromVersionId { get; set; }
}