using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Model.Chords;

namespace HarmonyDB.Shelf.Model.AnalysisTools;

/// <summary>
/// A key the index detected, with its probability (0–1). The key is the chord sheet's: its tonic as written ("F#",
/// "Bb") and whether it is minor.
/// </summary>
public sealed record DetectedKey
{
    public required SheetKey Key { get; init; }

    public required float Confidence { get; init; }
}

/// <summary>
/// The index's analysis of an item of chords. Its detected key is also what turns the circle of fifths (the circle
/// itself is drawn from the chords by the app).
/// </summary>
public sealed record ChordsAnalysis
{
    public required string Id { get; init; }

    /// <summary>
    /// Whether the item is open to the person; nothing else is given when it is not.
    /// </summary>
    public required ChordsState State { get; init; }

    /// <summary>
    /// Whether the index has analysed the item (it has not analysed every item, a new one or one of its own).
    /// </summary>
    public required bool Analysed { get; init; }

    /// <summary>
    /// The detected key, best first; a second one when it is nearly as likely (at least half as likely as the first).
    /// </summary>
    public required IReadOnlyList<DetectedKey> Keys { get; init; }

    /// <summary>
    /// The key the source states, as the index read it.
    /// </summary>
    public SheetKey? StatedKey { get; init; }

    /// <summary>
    /// How clearly the chords lean on one tonic, and on one scale (the index's scores).
    /// </summary>
    public float? TonicScore { get; init; }

    public float? ScaleScore { get; init; }

    /// <summary>
    /// The repeating progressions of the item, as the index found them.
    /// </summary>
    public required IReadOnlyList<ChordsLoop> Loops { get; init; }
}

/// <summary>
/// A repeating progression (a loop) in an item.
/// </summary>
public sealed record ChordsLoop
{
    /// <summary>
    /// The loop in the index's serialized form of a normalized progression (the app reads it with its port of the
    /// index's loop tools).
    /// </summary>
    public required string Normalized { get; init; }

    /// <summary>How many chords the loop has.</summary>
    public required int Length { get; init; }

    /// <summary>The note its normalized form starts from in this item ("C", "F#", "Bb").</summary>
    public required string NormalizationRoot { get; init; }

    /// <summary>How many times it occurs here, and how many times in a row; how much of the item it covers (0–1).</summary>
    public required float Occurrences { get; init; }

    public required float Successions { get; init; }

    public required float Coverage { get; init; }

    /// <summary>In how many items of the index it occurs.</summary>
    public required int SongsWithIt { get; init; }

    /// <summary>The loop's detected key in this item, best first.</summary>
    public required IReadOnlyList<DetectedKey> Keys { get; init; }
}

/// <summary>
/// Items with the same progression, as the person may see them.
/// </summary>
public sealed record SimilarChords
{
    public required int Page { get; init; }

    public required int TotalPages { get; init; }

    /// <summary>
    /// The items on this page that are open to the person (others are left out, unsaid).
    /// </summary>
    public required IReadOnlyList<SimilarChordsItem> Items { get; init; }
}

public sealed record SimilarChordsItem
{
    public required ChordsHeader Header { get; init; }

    /// <summary>How much of the item the progression covers (0–1).</summary>
    public required float Coverage { get; init; }

    public SheetKey? DetectedKey { get; init; }
}
