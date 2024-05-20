using OneShelf.Authorization.Api.Model;

namespace OneShelf.Collectives.Api.Model.V2;

public class GetRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required Guid CollectiveId { get; set; }
}