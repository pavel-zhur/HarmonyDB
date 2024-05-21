using OneShelf.Authorization.Api.Model;
using OneShelf.Collectives.Api.Model.V2.Sub;

namespace OneShelf.Collectives.Api.Model.V2;

public class UpdateRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public Guid CollectiveId { get; set; }

    public required Collective Collective { get; set; }
}