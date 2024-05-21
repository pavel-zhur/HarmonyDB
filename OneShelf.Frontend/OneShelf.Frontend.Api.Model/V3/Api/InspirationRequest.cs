using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class InspirationRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }
}