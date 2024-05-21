using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetVisitedChordsRequest : IRequestWithIdentity
{
    public Identity Identity { get; set; }

    public int PageIndex { get; set; }
}