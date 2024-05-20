using OneShelf.Authorization.Api.Model;

namespace OneShelf.Sources.Self.Api.Model.V1;

public class FormatPreviewRequest
{
    public required string Content { get; init; }
}