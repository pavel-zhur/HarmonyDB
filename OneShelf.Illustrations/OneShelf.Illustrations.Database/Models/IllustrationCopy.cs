namespace OneShelf.Illustrations.Database.Models;

/// <summary>
/// A resized copy of an illustration's image, a square JPEG of one of <see cref="IllustrationsDatabase.CopySizes"/>.
/// </summary>
public class IllustrationCopy
{
    public required Guid IllustrationId { get; init; }

    public required int Size { get; init; }

    public required byte[] Image { get; init; }
}
