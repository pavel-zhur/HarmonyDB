using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class FormatPreviewRequest : IRequestWithIdentity
{
    public required Identity Identity { get; init; }

    public required string Content { get; init; }
}