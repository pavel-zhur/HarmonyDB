namespace HarmonyDB.Source.Api.Model.V2.Contents;

/// <summary>
/// The words of the recording, by lines and words, with their times where they are known.
/// </summary>
public record Lyrics
{
    /// <summary>
    /// The language, as a two-letter code ("en", "ru"), when known.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// The lines in the order they are sung.
    /// </summary>
    public required IReadOnlyList<LyricsLine> Lines { get; init; }

    /// <summary>
    /// What was heard sung that no line accounts for (a verse left out of the written words), in time order.
    /// </summary>
    public IReadOnlyList<LyricsFragment> Unwritten { get; init; } = [];
}

public record LyricsLine
{
    public required string Text { get; init; }

    /// <summary>
    /// When the line begins and ends; both null when its time is not known.
    /// </summary>
    public double? Start { get; init; }

    public double? End { get; init; }

    /// <summary>
    /// Its words in order; joined, they are <see cref="Text"/>. Empty when the material times lines only.
    /// </summary>
    public IReadOnlyList<LyricsWord> Words { get; init; } = [];

    /// <summary>
    /// How the written line was found in the singing, when the words were written first and placed on the recording.
    /// </summary>
    public LyricsLineMatch? Match { get; init; }
}

public record LyricsWord
{
    /// <summary>
    /// The word as written, with the space before it where there is one.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// When it is sung; null when not placed. <see cref="End"/> is never given without <see cref="Start"/>.
    /// </summary>
    public double? Start { get; init; }

    public double? End { get; init; }
}

public record LyricsFragment
{
    public required string Text { get; init; }

    public double? Start { get; init; }

    public double? End { get; init; }
}

public enum LyricsLineMatch
{
    /// <summary>The line was heard sung.</summary>
    Heard,

    /// <summary>Not heard clearly, but placed between the lines around it: less certain.</summary>
    Inferred,

    /// <summary>Not found in the singing: it has no times.</summary>
    NotFound,
}
