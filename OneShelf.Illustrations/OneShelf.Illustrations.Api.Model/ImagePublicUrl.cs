namespace OneShelf.Illustrations.Api.Model;

public class ImagePublicUrl
{
    public required Guid Id { get; init; }

    public Uri? Url1024 { get; init; }

    public Uri? Url512 { get; init; }

    public Uri? Url256 { get; init; }

    public Uri? Url128 { get; init; }
}