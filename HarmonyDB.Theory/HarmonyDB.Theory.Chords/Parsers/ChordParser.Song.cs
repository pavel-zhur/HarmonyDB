using System.Text.RegularExpressions;
using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using OneShelf.Common;

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

    /// <summary>
    /// How much better (by the share of their notes in the key) the chords with the bare B must fit the key as B flat to be read so.
    /// Chosen on songs with a known answer: 40 % of the German-written found, 0.2 % of the English ones taken for German.
    /// </summary>
    private const double MinKeyGainAsBFlat = 0.5;

    /// <summary>
    /// How many more notes (per occurrence, the near neighbours weighing double) the chords with the bare B must share with their neighbours as B flat.
    /// </summary>
    private const double MinSurroundingsGainAsBFlat = 0.25;

    private static readonly IReadOnlyList<byte> MajorScale = [0, 2, 4, 5, 7, 9, 11];

    // The natural minor with the leading tone.
    private static readonly IReadOnlyList<byte> MinorScale = [0, 2, 3, 5, 7, 8, 10, 11];

    // The German names that are not English words: Cis, Fis, Gism, Des, Ges, fis…
    private static readonly Regex GermanName = new("^([A-Ga-g]is|[CDFGHcdfgh]es)(is|es)?(?![a-ln-z])", RegexOptions.Compiled);

    // The chord written with a variant number: "G2" beside "G1".
    private static readonly Regex Variant = new(@"^(.*\D)([1-9])$", RegexOptions.Compiled);

    /// <summary>
    /// Parses the chord knowing the other chords of its song (in their order); needed when some meanings are left to be detected (<see cref="ChordParsingOptions.NeedsSong"/>).
    /// </summary>
    public static ChordParseResult Parse(string stringRepresentation, IEnumerable<string> songChords, ChordParsingOptions? options = null)
        => ParseSong(songChords.Append(stringRepresentation), options)[stringRepresentation];

    /// <summary>
    /// Parses the chords of a song, given in their order in the song.
    /// The meanings left to be detected (<see cref="NotationUse.Detect"/>, <see cref="HHandling.Detect"/>, <see cref="PlusSeventhMeaning.Detect"/>,
    /// <see cref="BareNinthMeaning.Detect"/>) are decided from the song; with <see cref="ChordTypeParsingOptions.IgnoreVariantNumbers"/>,
    /// the chords numbered as variants of one chord ("G1", "G2", "G3") are read as that chord.
    /// </summary>
    public static IReadOnlyDictionary<string, ChordParseResult> ParseSong(IEnumerable<string> songChords, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;
        var sequence = songChords.ToList();
        var distinct = sequence.Distinct().ToList();

        var resolved = ResolveNotation(sequence, options);

        // The variants: "G2", "G3" beside "G1" are G.
        string Unvariant(string chord)
            => resolved.ChordTypeParsingOptions.IgnoreVariantNumbers && Variant.Match(chord) is { Success: true } match && distinct.Contains(match.Groups[1].Value + "1")
                ? match.Groups[1].Value
                : chord;

        if (resolved.ChordTypeParsingOptions.PlusSeventhMeaning == PlusSeventhMeaning.Detect)
            resolved.ChordTypeParsingOptions.PlusSeventhMeaning = DetectPlusSeventhMeaning(sequence.Select(Unvariant).ToList(), resolved);

        if (resolved.ChordTypeParsingOptions.BareNinthMeaning != BareNinthMeaning.Detect)
            return distinct.ToDictionary(x => x, x => Parse(Unvariant(x), resolved));

        // The bare ninths: added when their seventh is out of the key the other chords make.
        var dominant = resolved.Clone();
        dominant.ChordTypeParsingOptions.BareNinthMeaning = BareNinthMeaning.Dominant;
        var added = resolved.Clone();
        added.ChordTypeParsingOptions.BareNinthMeaning = BareNinthMeaning.Added;

        var results = distinct.ToDictionary(x => x, x => Parse(Unvariant(x), dominant));
        bool IsBareNinth(ChordParseResult result) => result.Trace.ChordTypeParseLogic?.HasFlag(ChordTypeParseLogic.BareNinth) == true;

        var keyChords = results.Values.Where(x => x.Chord != null && !IsBareNinth(x)).Select(x => GetPitchClasses(x.Chord!)).ToList();
        var key = keyChords.Any() ? GetKeyFits(keyChords, [MajorScale]).MaxBy(x => x.fit).key : null;
        foreach (var chord in distinct.Where(x => IsBareNinth(results[x])))
        {
            if (key != null && !key.Contains(Note.Normalize(results[chord].Chord!.Root.Value + 10)))
                results[chord] = Parse(Unvariant(chord), added);
        }

        return results;
    }

    /// <summary>
    /// What the bare B means in the song (the chords given in their order): <see cref="HHandling.BMeansH"/> or <see cref="HHandling.BbMeansH"/>.
    /// German when the song uses H or German note names. Otherwise English, unless all of these hold:
    /// the song does not spell B flat explicitly ("Bb", "A#");
    /// the other chords make a clear key, and in at least one of the keys they fit best they all stand on the degrees of the key;
    /// the chords with the bare B fit the best keys clearly better as B flat;
    /// and the chords around them in the song agree: as B flat they share more notes with their neighbours.
    /// </summary>
    public static HHandling DetectHHandling(IEnumerable<string> songChords, ChordParsingOptions? options = null)
    {
        options ??= ChordParsingOptions.Default;
        var sequence = songChords.ToList();
        var notation = options.Clone();
        notation.NoteParsingOptions.HHandling = HHandling.BMeansH;
        notation = ResolveNotation(sequence, notation);
        if (notation.NoteParsingOptions.GermanNoteNames == NotationUse.Always)
            return HHandling.BbMeansH;

        var englishOptions = WithoutChordTypeDetection(notation);
        var germanOptions = englishOptions.Clone();
        germanOptions.NoteParsingOptions.HHandling = HHandling.BbMeansH;

        var chords = sequence
            .Distinct()
            .ToDictionary(x => x, x => (english: Parse(x, englishOptions).Chord, german: Parse(x, germanOptions).Chord));

        // H is written in the German notation, where the bare B is B flat.
        if (chords.Values.Any(x => (x.english ?? x.german) is { } chord
                                   && (chord.RootRepresentation.NaturalNoteRepresentation == NaturalNoteRepresentation.H
                                       || chord.BassRepresentation?.NaturalNoteRepresentation == NaturalNoteRepresentation.H)))
            return HHandling.BbMeansH;

        var affected = chords.Where(x => x.Value.english != null && x.Value.german != null && !x.Value.english.Equals(x.Value.german)).ToDictionary(x => x.Key, x => (x.Value.english!, x.Value.german!));
        var others = chords.Where(x => x.Value.english != null && x.Value.english.Equals(x.Value.german)).ToDictionary(x => x.Key, x => x.Value.english!);

        if (!affected.Any() || others.Count < MinKeyChords)
            return HHandling.BMeansH;

        // B flat is spelled explicitly elsewhere in the song ("Bb", "A#"): the author tells them apart.
        var bFlat = NoteParser.ParseNote("B", germanOptions.NoteParsingOptions);
        if (others.Values.Any(x => x.Root.Equals(bFlat) || x.Bass.Equals(bFlat)))
            return HHandling.BMeansH;

        // The keys the other chords fit best (the ties and those one note short of the best), if they fit them well.
        var keyChords = others.Values.Select(GetPitchClasses).ToList();
        var fits = GetKeyFits(keyChords, [MajorScale, MinorScale]);
        var bestFit = fits.Max(x => x.fit);
        if (bestFit < MinKeyFit * keyChords.Sum(chord => chord.Count))
            return HHandling.BMeansH;

        bool InKey(HashSet<byte> key, ChordRepresentation chord) => key.Contains(chord.Root.Value) && (!chord.Bass.HasValue || key.Contains(chord.Bass.Value.Value));

        // In at least one of the keys they fit best (the ties and those one note short of the best) all the other chords stand on the degrees of the key:
        // in a song that modulates or wanders, a B out of the key tells nothing.
        if (!fits.Where(x => x.fit >= bestFit - 1).Any(x => others.Values.All(chord => InKey(x.key, chord))))
            return HHandling.BMeansH;

        // The chords with the bare B fit the best keys clearly better as B flat: by the share of their notes in the key.
        var bestKeys = fits.Where(x => x.fit == bestFit).Select(x => x.key).ToList();
        double InBestKeys(ChordRepresentation chord) => bestKeys.Average(key => (double)GetPitchClasses(chord).Count(key.Contains) / GetPitchClasses(chord).Count);
        if (affected.Values.Average(x => InBestKeys(x.Item2) - InBestKeys(x.Item1)) < MinKeyGainAsBFlat)
            return HHandling.BMeansH;

        // And the chords around them in the song agree: as B flat they share more notes with their neighbours.
        double Surroundings(int position, ChordRepresentation chord)
        {
            var notes = GetPitchClasses(chord);
            var score = 0.0;
            foreach (var (offset, weight) in new[] { (-2, 0.5), (-1, 1.0), (1, 1.0), (2, 0.5) })
            {
                var index = position + offset;
                if (index >= 0 && index < sequence.Count && others.TryGetValue(sequence[index], out var neighbour))
                    score += weight * notes.Count(GetPitchClasses(neighbour).Contains) / notes.Count;
            }

            return score;
        }

        var occurrences = sequence.WithIndices().Where(x => affected.ContainsKey(x.x)).ToList();
        var surroundingsGain = occurrences.Average(x => Surroundings(x.i, affected[x.x].Item2) - Surroundings(x.i, affected[x.x].Item1));

        return surroundingsGain > MinSurroundingsGainAsBFlat ? HHandling.BbMeansH : HHandling.BMeansH;
    }

    /// <summary>
    /// Decides the note notations left to be detected: the solfège, the German names, the meaning of B.
    /// </summary>
    private static ChordParsingOptions ResolveNotation(IReadOnlyList<string> sequence, ChordParsingOptions options)
    {
        var resolved = options.Clone();
        var noteOptions = resolved.NoteParsingOptions;

        if (noteOptions.Solfege == NotationUse.Detect)
        {
            // The songs written in solfège use the names that are not letters: Re, Mi, Fa, Sol, La, Si.
            var solfege = noteOptions.Clone();
            solfege.Solfege = NotationUse.Always;
            solfege.GermanNoteNames = NotationUse.Never;
            solfege.HHandling = HHandling.BMeansH;
            var solfegeOptions = WithNoteOptions(resolved, solfege);

            bool IsSolfegeOtherThanDo(string chord)
            {
                var result = Parse(chord, solfegeOptions);
                return result.Chord?.RootRepresentation.NaturalNoteRepresentation is >= NaturalNoteRepresentation.La and <= NaturalNoteRepresentation.Sol
                       && result.Chord.RootRepresentation.NaturalNoteRepresentation != NaturalNoteRepresentation.Do;
            }

            noteOptions.Solfege = sequence.Distinct().Any(IsSolfegeOtherThanDo) ? NotationUse.Always : NotationUse.Never;
        }

        if (noteOptions.GermanNoteNames == NotationUse.Detect)
            noteOptions.GermanNoteNames = sequence.Any(x => GermanName.IsMatch(x)) ? NotationUse.Always : NotationUse.Never;

        if (noteOptions.HHandling == HHandling.Detect)
            noteOptions.HHandling = DetectHHandling(sequence, resolved);

        return resolved;
    }

    /// <summary>
    /// "7+" is the augmented fifth in the song when the song spells the major seventh otherwise ("Cmaj7", "CM7", "C7M", "Cmaj"),
    /// or when a "7+" chord follows the plain seventh chord on the same root ("A7 A7+": the rising fifth); the major seventh otherwise.
    /// </summary>
    private static PlusSeventhMeaning DetectPlusSeventhMeaning(IReadOnlyList<string> sequence, ChordParsingOptions options)
    {
        var majorSeventh = options.Clone();
        majorSeventh.ChordTypeParsingOptions.PlusSeventhMeaning = PlusSeventhMeaning.MajorSeventh;
        majorSeventh = WithoutChordTypeDetection(majorSeventh);

        var results = sequence.Distinct().ToDictionary(x => x, x => Parse(x, majorSeventh));
        bool IsPlusSeventh(ChordParseResult result) => result.Trace.ChordTypeParseLogic?.HasFlag(ChordTypeParseLogic.PlusRaisedSeventh) == true;

        if (!results.Values.Any(IsPlusSeventh))
            return PlusSeventhMeaning.MajorSeventh;

        if (results.Values.Any(x => x.Chord?.ChordType.Extension >= ChordTypeExtension.XMaj7 && !IsPlusSeventh(x)))
            return PlusSeventhMeaning.AugmentedFifth;

        for (var i = 1; i < sequence.Count; i++)
        {
            if (IsPlusSeventh(results[sequence[i]])
                && results[sequence[i - 1]].Chord is { ChordType: { Type: ChordMainType.Major, Extension: ChordTypeExtension.X7, Additions: ChordTypeAdditions.None } } previous
                && previous.Root.Equals(results[sequence[i]].Chord!.Root))
                return PlusSeventhMeaning.AugmentedFifth;
        }

        return PlusSeventhMeaning.MajorSeventh;
    }

    private static ChordParsingOptions WithNoteOptions(ChordParsingOptions options, NoteParsingOptions noteOptions)
    {
        var result = WithoutChordTypeDetection(options);
        result.NoteParsingOptions = noteOptions;
        return result;
    }

    // For the detection: the chord type meanings that are not yet decided are taken in their default readings.
    private static ChordParsingOptions WithoutChordTypeDetection(ChordParsingOptions options)
    {
        var result = options.Clone();
        if (result.ChordTypeParsingOptions.PlusSeventhMeaning == PlusSeventhMeaning.Detect)
            result.ChordTypeParsingOptions.PlusSeventhMeaning = PlusSeventhMeaning.MajorSeventh;
        if (result.ChordTypeParsingOptions.BareNinthMeaning == BareNinthMeaning.Detect)
            result.ChordTypeParsingOptions.BareNinthMeaning = BareNinthMeaning.Dominant;
        return result;
    }

    private static List<(HashSet<byte> key, int fit)> GetKeyFits(IReadOnlyList<IReadOnlyList<byte>> chords, IReadOnlyList<IReadOnlyList<byte>> scales)
        => Enumerable.Range(0, NoteConstants.TotalNotes)
            .SelectMany(tonic => scales.Select(scale => scale.Select(x => (byte)((x + tonic) % NoteConstants.TotalNotes)).ToHashSet()))
            .Select(key => (key, fit: chords.Sum(chord => chord.Count(key.Contains))))
            .ToList();

    private static IReadOnlyList<byte> GetPitchClasses(ChordRepresentation chord)
        => chord.GetNotes()
            .Select(x => x.Value)
            .Concat(chord.Bass.HasValue ? [chord.Bass.Value.Value] : [])
            .Distinct()
            .Order()
            .ToList();
}
