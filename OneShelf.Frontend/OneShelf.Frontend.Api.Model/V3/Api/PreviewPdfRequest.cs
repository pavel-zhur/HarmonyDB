using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class PreviewPdfRequest : IRequestWithIdentity
{
    public required GetPdfsChunkRequestFile File { get; init; }

    public required long UserId { get; init; }

    public required string Hash { get; init; }

    public required long Expiration { get; init; }

    Identity IRequestWithIdentity.Identity => throw new InvalidOperationException("Not supported for this kind of request.");
}