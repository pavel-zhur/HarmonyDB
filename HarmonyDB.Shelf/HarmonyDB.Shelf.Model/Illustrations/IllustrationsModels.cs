namespace HarmonyDB.Shelf.Model.Illustrations;

/// <summary>
/// What the illustrations can be drawn by: the presets the server has.
/// </summary>
public record IllustrationPresetList
{
    /// <summary>
    /// The presets' keys, the plain one first; the frontend words them ("plain"; the others are moods: anger, sadness,
    /// drama …, by their keys).
    /// </summary>
    public required IReadOnlyList<string> Presets { get; init; }
}

public record IllustrationImage
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The preset it was drawn by; null for one drawn otherwise (earlier, by other means).
    /// </summary>
    public string? Preset { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The square picture in each size there is, by its side in pixels: <c>GET</c> of the address.
    /// </summary>
    public required IReadOnlyDictionary<int, string> Urls { get; init; }
}

/// <summary>
/// A drawing going on for the chords, whoever ordered it.
/// </summary>
public record IllustrationDrawing
{
    public required string Preset { get; init; }

    /// <summary>
    /// The job, when the person ordered it (<c>/api/jobs/{id}</c>).
    /// </summary>
    public Guid? JobId { get; init; }
}

public record ChordsIllustrations
{
    public required string ChordsId { get; init; }

    /// <summary>
    /// The earliest first.
    /// </summary>
    public required IReadOnlyList<IllustrationImage> Images { get; init; }

    /// <summary>
    /// Present while a batch is being drawn.
    /// </summary>
    public IllustrationDrawing? Drawing { get; init; }
}

public record DrawIllustrationsRequest
{
    /// <summary>
    /// One of the presets' keys.
    /// </summary>
    public required string Preset { get; init; }
}

/// <summary>
/// What a job of the kind "illustrations" ends with.
/// </summary>
public record IllustrationsJobResult
{
    /// <summary>
    /// How many pictures were drawn now (none when the preset was drawn for these chords before).
    /// </summary>
    public required int Made { get; init; }
}

public static class IllustrationsErrorCodes
{
    /// <summary>400: no such preset.</summary>
    public const string UnknownPreset = "unknown_preset";

    /// <summary>404: the chords are not open to the person.</summary>
    public const string ChordsNotOpen = "chords_not_open";

    /// <summary>429: the person has drawn as many pictures as one may in a day (details: <c>limit</c>, <c>used</c>).</summary>
    public const string DailyLimit = "illustrations_daily_limit";

    /// <summary>A job's failure: the chords have no words to draw by.</summary>
    public const string NoLyrics = "no_lyrics";

    /// <summary>A job's failure: nothing came out to be drawn; ordering again may help.</summary>
    public const string NothingToDraw = "nothing_to_draw";
}
