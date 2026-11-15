using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;

namespace HarmonyDB.Theory.Chords.Parsers;

public static partial class ChordParser
{
    /// <summary>
    /// The minimum number of the song's distinct chords that do not depend on the meaning of B, to tell the key of the song.
    /// </summary>
    private const int MinKeyChords = 3;

    /// <summary>
    /// The minimum share of the notes of those chords that the key must contain, for the key to be clear.
    /// </summary>
    private const double MinKeyFit = 0.85;

    private static readonly IReadOnlyList<byte> MajorScale = [0, 2, 4, 5, 7, 9, 11];

    // The natural minor with the leading tone.
    private static readonly IReadOnlyList<byte> MinorScale = [0, 2, 3, 5, 7, 8, 10, 11];

    /// <summary>
    /// Parses the chord knowing the other chords of its song (see <see cref="ChordParsingOptions.DetectHHandlingFromSong"/>).
    /// </summary>
    public static ChordParseResult Parse(string stringRepresentation, IEnumerable<string> songChords, ChordParsingOptions? options = null)
        => ParseSong(songChords.Append(stringRepresentation), options)[stringRepresentation];

    /// <summary>
    /// Parses the distinct chords of a song together (see <see cref="ChordParsingOptions.DetectHHandlingFromSong"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, ChordParseResult> ParseSong(IEnumerable<string> songChords, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;
        var distinct = songChords.Distinct().ToList();

        if (options.DetectHHandlingFromSong)
            options = options.WithHHandling(DetectHHandling(distinct, options));

        return distinct.ToDictionary(x => x, x => Parse(x, options));
    }

    /// <summary>
    /// What the bare B means in the song, judging by its distinct chords.
    /// The <see cref="NoteParsingOptions.HHandling"/> of the options stays, unless the song uses H (then B is B flat),
    /// or all of these hold: the song does not spell the other meaning of B explicitly ("Bb", "A#" or "H"),
    /// the other chords make a clear key, all the chords with the bare B have their roots and basses out of it,
    /// and with the other meaning of B all of them are in it.
    /// </summary>
    public static HHandling DetectHHandling(IEnumerable<string> songChords, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;
        var current = options.NoteParsingOptions.HHandling;
        var alternative = current == HHandling.BbMeansH ? HHandling.BMeansH : HHandling.BbMeansH;
        var currentOptions = options.WithHHandling(current);
        var alternativeOptions = options.WithHHandling(alternative);

        var chords = songChords
            .Distinct()
            .Select(x => (current: Parse(x, currentOptions).Chord, alternative: Parse(x, alternativeOptions).Chord))
            .Where(x => x.current != null || x.alternative != null)
            .ToList();

        // H is written in the German notation, where the bare B is B flat.
        if (chords.Any(x => (x.current ?? x.alternative)!.RootRepresentation.NaturalNoteRepresentation == NaturalNoteRepresentation.H
                            || (x.current ?? x.alternative)!.BassRepresentation?.NaturalNoteRepresentation == NaturalNoteRepresentation.H))
            return HHandling.BbMeansH;

        var affected = chords
            .Where(x => x.current != null && x.alternative != null && !x.current.Equals(x.alternative))
            .Select(x => (current: x.current!, alternative: x.alternative!))
            .ToList();

        var others = chords
            .Where(x => x.current != null && x.alternative != null && x.current.Equals(x.alternative))
            .Select(x => x.current!)
            .ToList();

        if (!affected.Any() || others.Count < MinKeyChords)
            return current;

        // The other meaning of the bare B is spelled explicitly elsewhere in the song ("Bb", "A#"): the author tells them apart.
        var alternativeB = NoteParser.ParseNote("B", alternativeOptions.NoteParsingOptions);
        if (others.Any(x => x.Root.Equals(alternativeB) || x.Bass.Equals(alternativeB)))
            return current;

        // The keys the other chords fit best (the ties and those one note short of the best), if they fit them well.
        var keyChords = others.Select(GetPitchClasses).ToList();
        var notesCount = keyChords.Sum(chord => chord.Count);
        var fits = Enumerable.Range(0, NoteConstants.TotalNotes)
            .SelectMany(tonic => new[] { MajorScale, MinorScale }.Select(scale => scale.Select(x => (byte)((x + tonic) % NoteConstants.TotalNotes)).ToHashSet()))
            .Select(key => (key, fit: keyChords.Sum(chord => chord.Count(key.Contains))))
            .ToList();

        var bestFit = fits.Max(x => x.fit);
        if (bestFit < MinKeyFit * notesCount)
            return current;

        bool InKey(HashSet<byte> key, ChordRepresentation chord) => key.Contains(chord.Root.Value) && (!chord.Bass.HasValue || key.Contains(chord.Bass.Value.Value));

        var keys = fits
            .Where(x => x.fit >= bestFit - 1)
            .Select(x => x.key)
            .ToList();

        // In at least one of these keys all the other chords stand on the degrees of the key: in a song that modulates or wanders, a B out of the key tells nothing.
        if (!keys.Any(key => others.All(x => InKey(key, x))))
            return current;

        // In each of these keys, all the chords with the bare B are out of the key (by their roots and basses), and all fit it with the other meaning of B.
        return keys.All(key => affected.All(x => !InKey(key, x.current) && InKey(key, x.alternative))) ? alternative : current;
    }

    private static IReadOnlyList<byte> GetPitchClasses(ChordRepresentation chord)
        => chord.GetNotes()
            .Select(x => x.Value)
            .Concat(chord.Bass.HasValue ? [chord.Bass.Value.Value] : [])
            .Distinct()
            .Order()
            .ToList();
}
