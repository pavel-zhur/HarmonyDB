using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class DeleteLikeCategoryRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required int LikeCategoryId { get; set; }
}