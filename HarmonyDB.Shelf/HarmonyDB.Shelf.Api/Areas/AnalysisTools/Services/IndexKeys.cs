using HarmonyDB.Common.Representations.OneShelf;

namespace HarmonyDB.Shelf.Api.Areas.AnalysisTools.Services;

/// <summary>
/// The index's numbering of notes and keys, turned into the chord sheet's keys — the one place that reads it.
/// A note is a <see cref="Note.Value"/> (A is 0, C is 3 … G♯ is 11); a key is numbered tonic × 2 + (minor ? 1 : 0).
/// A tonic is written as the key is usually written: Bb, Eb, Ab, Db and F# majors; C#, F#, G# minors, and Bb, Eb minors.
/// </summary>
public static class IndexKeys
{
    private static readonly string[] MajorTonics = ["A", "Bb", "B", "C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab"];

    private static readonly string[] MinorTonics = ["A", "Bb", "B", "C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#"];

    /// <param name="index">The index's number of a key.</param>
    public static SheetKey Key(int index) => Key((byte)(index / 2), index % 2 == 1);

    public static SheetKey Key(byte tonic, bool isMinor) => new(isMinor ? MinorTonics[tonic] : MajorTonics[tonic], isMinor);

    /// <summary>
    /// A note's name, as a major key's tonic is written.
    /// </summary>
    public static string NoteName(byte note) => MajorTonics[note];

    /// <summary>
    /// A loop's key in an item: the index keeps a loop's keys relative to its normalized form, which starts in the item
    /// from <paramref name="normalizationRoot"/>.
    /// </summary>
    /// <param name="index">The index's number of the loop's key.</param>
    public static SheetKey LoopKey(int index, byte normalizationRoot)
        => Key(Note.Normalize(normalizationRoot - index / 2), index % 2 == 1);
}
