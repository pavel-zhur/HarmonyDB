namespace OneShelf.Illustrations.Api.Model;

/// <summary>
/// The presets the new shelf offers: the illustrations of one system message, plain or in a mood.
/// </summary>
public class ShelfPresetsResponse
{
    /// <summary>
    /// The presets' keys, the plain one first.
    /// </summary>
    public required List<string> Presets { get; init; }

    /// <summary>
    /// The sides of the square copies every image has, at <see cref="IllustrationsApiUrls.Images"/>.
    /// </summary>
    public required List<int> Sizes { get; init; }
}

public class ShelfReadRequest
{
    /// <summary>
    /// What the illustrations belong to: for the new shelf, the chords' id.
    /// </summary>
    public required string Key { get; init; }
}

public class ShelfReadResponse
{
    /// <summary>
    /// The images, the earliest first.
    /// </summary>
    public required List<ShelfImage> Images { get; init; }
}

public class ShelfImage
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The preset it was made by; null for one made otherwise.
    /// </summary>
    public string? Preset { get; init; }

    public required DateTime CreatedOn { get; init; }
}

public class ShelfGenerateRequest
{
    public required string Key { get; init; }

    /// <summary>
    /// The song's lyrics, used when no prompts are kept for the key yet.
    /// </summary>
    public required string Lyrics { get; init; }

    public required string Preset { get; init; }

    public string? AdditionalBillingInfo { get; init; }
}

public enum ShelfGenerateOutcome
{
    /// <summary>The images are made.</summary>
    Made,

    /// <summary>The images of the preset were made before: nothing is made again.</summary>
    AlreadyMade,

    /// <summary>There are no lyrics to draw by.</summary>
    NoLyrics,

    /// <summary>The prompts asked for nothing to draw.</summary>
    NoPrompts,
}

public class ShelfGenerateResponse
{
    public required ShelfGenerateOutcome Outcome { get; init; }

    /// <summary>
    /// How many images were made now.
    /// </summary>
    public required int Made { get; init; }
}
