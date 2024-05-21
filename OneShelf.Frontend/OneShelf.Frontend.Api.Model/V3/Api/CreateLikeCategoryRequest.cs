using OneShelf.Authorization.Api.Model;
using OneShelf.Frontend.Api.Model.V3.Databasish;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class CreateLikeCategoryRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required LikeCategory LikeCategory { get; set; }
}