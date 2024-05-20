using OneShelf.Authorization.Api.Model;

namespace OneShelf.Collectives.Api.Model.V2;

public class ListRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }
}