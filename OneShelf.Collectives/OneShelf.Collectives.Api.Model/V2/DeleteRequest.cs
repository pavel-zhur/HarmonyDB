using OneShelf.Authorization.Api.Model;

namespace OneShelf.Collectives.Api.Model.V2;

public class DeleteRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public Guid CollectiveId { get; set; }
}