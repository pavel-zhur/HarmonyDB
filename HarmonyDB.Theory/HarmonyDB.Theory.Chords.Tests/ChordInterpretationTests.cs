using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class ChordInterpretationTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Real chords (all of them occur in <see cref="Resources.AllChords"/>) and their canonical names, read with the default meanings.
    /// </summary>
    public static readonly TheoryData<string, string> CanonicalData = new()
    {
        // Triads, sixths, sevenths.
        { "C", "C" },
        { "Am", "Am" },
        { "Cmin7", "Cm7" },
        { "C5", "C5" },
        { "C6", "C6" },
        { "Cm6", "Cm6" },
        { "G7", "G7" },
        { "Cmaj7", "Cmaj7" },
        { "CM7", "Cmaj7" },
        { "C7M", "Cmaj7" },
        { "Cmmaj7", "Cmmaj7" },
        { "Cm(maj7)", "Cmmaj7" },

        // Diminished, half-diminished, augmented.
        { "Cdim", "Cdim" },
        { "Cdim7", "Cdim7" },
        { "Co", "Cdim7" },
        { "C°", "Cdim7" },
        { "C7dim", "Cdim7" },
        { "Cm7b5", "Cm7b5" },
        { "Cm7-5", "Cm7b5" },
        { "Cm75-", "Cm7b5" },
        { "Bm7/5-", "Bm7b5" },
        { "Am5-/7", "Am7b5" },
        { "Cm-5", "Cdim" },
        { "Cm5-", "Cdim" },
        { "Caug", "Caug" },
        { "C+", "Caug" },
        { "C+5", "Caug" },
        { "C7#5", "C7#5" },
        { "C7+5", "C7#5" },
        { "C75+", "C7#5" },
        { "Am#5", "Am(#5)" },
        { "Am+", "Am(#5)" },

        // The flat fifth.
        { "C(b5)", "C(b5)" },
        { "C-5", "C(b5)" },
        { "C5-", "C(b5)" },

        // The plus and the seventh.
        { "C7+", "Cmaj7" },
        { "C+7", "Cmaj7" },
        { "Cm+7", "Cmmaj7" },
        { "Cm7+", "Cmmaj7" },

        // Suspensions.
        { "Csus", "Csus4" },
        { "Csus4", "Csus4" },
        { "C4", "Csus4" },
        { "Csus2", "Csus2" },
        { "C2", "Csus2" },
        { "Csus9", "Csus2" },
        { "Amsus2", "Asus2" },
        { "Amsus4", "Asus4" },
        { "C7sus4", "C7sus4" },
        { "C7/4", "C7sus4" },
        { "C9sus4", "C9sus4" },
        { "C6sus4", "C6sus4" },

        // Added and upper degrees.
        { "Cadd9", "Cadd9" },
        { "C(9)", "Cadd9" },
        { "C/9", "Cadd9" },
        { "Am/9", "Amadd9" },
        { "Cmadd9", "Cmadd9" },
        { "Cm(add9)", "Cmadd9" },
        { "C6/9", "C6add9" },
        { "C69", "C6add9" },
        { "Cm6/9", "Cm6add9" },
        { "C9", "C9" },
        { "C7/9", "C9" },
        { "C7(9)", "C9" },
        { "Am7/9", "Am9" },
        { "C11", "C11" },
        { "C13", "C13" },
        { "C7/6", "C7add13" },
        { "C7add11", "C7add11" },
        { "Cadd11+", "Cadd#11" },
        { "Cmaj7#11", "Cmaj7#11" },
        { "C7#11", "C7#11" },
        { "Cmaj6", "C6" },
        { "CM6", "C6" },

        // Altered ninths.
        { "C7b9", "C7b9" },
        { "C7-9", "C7b9" },
        { "C9-", "C7b9" },
        { "C-9", "C7b9" },
        { "Cadd9-", "Caddb9" },
        { "C7#9", "C7#9" },
        { "C7+9", "C7#9" },
        { "C+9", "C7#9" },
        { "E7/9b", "E7b9" },
        { "C13b9", "C13b9" },
        { "C7b13", "C7b13" },
        { "C7+5-9", "C7#5b9" },
        { "C7#5#9", "C7#5#9" },

        // Minus as the minor.
        { "C-7", "Cm7" },

        // Misplaced root accidentals.
        { "Fm#", "F#m" },
        { "Bmb", "Bbm" },
        { "F5#", "F#5" },

        // Roots and basses.
        { "C/E", "C/E" },
        { "D/F#", "D/F#" },
        { "Am/G", "Am/G" },
        { "Am6/F#", "Am6/F#" },
        { "LA", "A" },
        { "LAm", "Am" },
        { "SOL7", "G7" },
        { "RE#m", "Ebm" },
        { "Do", "C" },
        { "Fadd9", "Fadd9" },

        // Forgiven decorations.
        { "C*", "C" },
        { "Am*", "Am" },
        { "Am?", "Am" },
        { "C'", "C" },
        { "G,", "G" },
        { "(G)", "G" },
        { "C(9", "Cadd9" },
        { "C(III)", "C" },
        { "Am(V)", "Am" },
    };

    [Theory]
    [MemberData(nameof(CanonicalData))]
    public void Canonical(string input, string expected)
    {
        var result = ChordParser.Parse(input, ChordParsingOptions.MostForgiving);
        Assert.Equal(ChordParseResultType.Success, result.Type);
        Assert.Equal(expected, result.Chord!.ToCanonical());
        testOutputHelper.WriteLine($"{result.Trace.ChordTypeParseLogic}");
    }

    public static readonly TheoryData<string, string> IntervalsData = new()
    {
        { "C", "0 4 7" },
        { "Cm", "0 3 7" },
        { "C5", "0 7" },
        { "Cdim", "0 3 6" },
        { "Cdim7", "0 3 6 9" },
        { "Cm7b5", "0 3 6 10" },
        { "Caug", "0 4 8" },
        { "C7#5", "0 4 8 10" },
        { "Csus2", "0 2 7" },
        { "Csus4", "0 5 7" },
        { "C6", "0 4 7 9" },
        { "C6/9", "0 2 4 7 9" },
        { "C7", "0 4 7 10" },
        { "Cmaj7", "0 4 7 11" },
        { "Cmmaj7", "0 3 7 11" },
        { "C9", "0 2 4 7 10" },
        { "C11", "0 2 4 5 7 10" },
        { "C13", "0 2 4 7 9 10" },
        { "C13b9", "0 1 4 7 9 10" },
        { "C7b9", "0 1 4 7 10" },
        { "C7#9", "0 3 4 7 10" },
        { "C7#5#9", "0 3 4 8 10" },
        { "Cadd9", "0 2 4 7" },
        { "Cmaj7#11", "0 4 6 7 11" },
        { "C7add11", "0 4 5 7 10" },
        { "C7/6", "0 4 7 9 10" },
        { "C(b5)", "0 4 6" },
    };

    [Theory]
    [MemberData(nameof(IntervalsData))]
    public void Intervals(string input, string expected)
    {
        var result = ChordParser.Parse(input, ChordParsingOptions.MostForgiving);
        Assert.Equal(expected, string.Join(" ", result.Chord!.ChordType.GetIntervals()));
    }

    [Fact]
    public void Notes()
    {
        var chord = ChordParser.Parse("D7/F#").Chord!;
        Assert.Equal("D F# A C", string.Join(" ", chord.GetNotes().Select(x => Constants.NoteConstants.CanonicalNames[x.Value])));
        Assert.Equal("F#", Constants.NoteConstants.CanonicalNames[chord.Bass!.Value.Value]);
    }

    /// <summary>
    /// Different notations of the same chord give equal chords and the same canonical name.
    /// </summary>
    public static readonly IEnumerable<object[]> SameChordData =
    [
        [new[] { "Cm7b5", "Cm7-5", "Cm75-", "Cm7(b5)", "Cø", "Cm5-/7" }],
        [new[] { "C7#5", "C7+5", "C75+", "Caug7", "C7(#5)" }],
        [new[] { "Cmaj7", "CM7", "C7M", "C7+", "C+7", "CΔ", "Cmaj" }],
        [new[] { "C9", "C7/9", "C7(9)", "C7add9" }],
        [new[] { "Cadd9", "C(9)", "C/9", "Cadd2" }],
        [new[] { "C6/9", "C69", "C6(9)", "C6add9" }],
        [new[] { "Cdim7", "Co", "C°", "C7dim", "Cdim(7)" }],
        [new[] { "C7b9", "C7-9", "C9-", "C7(b9)" }],
        [new[] { "Cm(maj7)", "Cmmaj7", "CmM7", "Cm+7", "Cm7+" }],
        [new[] { "Csus4", "Csus", "C4", "Cmsus4" }],
        [new[] { "F#m", "Gbm", "Fm#", "F#min", "F#mi" }],
        [new[] { "C6", "Cadd13", "Cadd6", "Cmaj6" }],
    ];

    [Theory]
    [MemberData(nameof(SameChordData))]
    public void SameChord(string[] inputs)
    {
        var results = inputs.Select(x => ChordParser.Parse(x, ChordParsingOptions.MostForgiving)).ToList();
        Assert.All(results, x => Assert.Equal(ChordParseResultType.Success, x.Type));
        Assert.Single(results.Select(x => x.Chord!.ToCanonical()).Distinct());
        Assert.Single(results.Select(x => x.Chord!.ChordType).Distinct());
    }

    /// <summary>
    /// Strings that are not chords (tab fragments, words, numbering), all of them from <see cref="Resources.AllChords"/>.
    /// </summary>
    public static readonly TheoryData<string, ChordParseError> ErrorsData = new()
    {
        { "E-0-0-0", ChordParseError.UnexpectedChordTypeToken },
        { "D-7-7-7", ChordParseError.EachExtensionTypeExpectedUnique },
        { "A-5-5-5", ChordParseError.DuplicateDegrees },
        { "A-2-2-2", ChordParseError.DuplicateAdditions },
        { "BE", ChordParseError.UnexpectedChordTypeToken },
        { "CD", ChordParseError.UnexpectedChordTypeToken },
        { "G3", ChordParseError.UnexpectedChordTypeToken },
        { "A-5-5", ChordParseError.DuplicateDegrees },
        { "D-7-", ChordParseError.UnboundAlteration },
        { "R", ChordParseError.UnreadableRoot },
    };

    /// <summary>
    /// Odd notations, all of them from <see cref="Resources.AllChords"/>, read by what their authors meant (see the songs in the research notes).
    /// </summary>
    public static readonly TheoryData<string, string> OddNotationsData = new()
    {
        { "Em5", "E5" },
        { "Bm5", "B5" },
        { "Bm7/5", "Bm7b5" },
        { "Dm7/5", "Dm7b5" },
        { "G#75", "Ab7" },
        { "D5maj7", "Dmaj7no3" },
        { "E5(7", "E7no3" },
        { "C(add", "Cadd9" },
        { "G#maj75", "Abmaj7" },
        { "D9/5", "D9" },
        { "Gadd5", "G" },
        { "Cadd5", "C" },
        { "A5#5", "A5(b6)" },
        { "F5(b5)", "F5add#11" },
        { "Cadd", "Cadd9" },
        { "C9add", "Cadd9" },
        { "Esus4add", "Esus4add9" },
        { "Eadd(#11)", "Eadd#11" },
        { "Em7-", "Em7" },
        { "Am7-", "Am7" },
        { "Fdd9", "Fadd9" },
        { "G1", "G" },
        { "A1", "A" },
        { "С#m", "C#m" },
    };

    [Theory]
    [MemberData(nameof(OddNotationsData))]
    public void OddNotations(string input, string expected)
    {
        var result = ChordParser.Parse(input, ChordParsingOptions.MostForgiving);
        Assert.Equal(ChordParseResultType.Success, result.Type);
        Assert.Equal(expected, result.Chord!.ToCanonical());
    }

    [Theory]
    [MemberData(nameof(ErrorsData))]
    public void Errors(string input, ChordParseError expected)
    {
        var result = ChordParser.Parse(input, ChordParsingOptions.MostForgiving);
        Assert.Equal(ChordParseResultType.Error, result.Type);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void NoChord()
    {
        Assert.Equal(ChordParseResultType.SpecialNoChord, ChordParser.Parse("N.C.").Type);
    }

    /// <summary>
    /// The forgiveness options are off by default.
    /// </summary>
    public static readonly TheoryData<string, ChordParseError> StrictErrorsData = new()
    {
        { "Fm#", ChordParseError.UnboundAlteration },
        { "G,", ChordParseError.BadSymbols },
        { "C*", ChordParseError.BadSymbols },
        { "C(9", ChordParseError.UnexpectedChordTypeToken },
        { "(G)", ChordParseError.UnreadableRoot },
        { "cm", ChordParseError.UnreadableRoot },
    };

    [Theory]
    [MemberData(nameof(StrictErrorsData))]
    public void StrictErrors(string input, ChordParseError expected)
    {
        var result = ChordParser.Parse(input);
        Assert.Equal(expected, result.Error);
    }

    private static ChordParsingOptions WithChordType(Action<ChordTypeParsingOptions> configure)
    {
        var chordTypeParsingOptions = new ChordTypeParsingOptions();
        configure(chordTypeParsingOptions);
        return new() { ChordTypeParsingOptions = chordTypeParsingOptions };
    }

    public static readonly IEnumerable<object[]> OptionsData =
    [
        ["C7+", WithChordType(_ => { }), "Cmaj7"],
        ["C7+", WithChordType(x => x.PlusSeventhMeaning = PlusSeventhMeaning.AugmentedFifth), "C7#5"],
        ["C+7", WithChordType(x => x.PlusSeventhMeaning = PlusSeventhMeaning.AugmentedFifth), "C7#5"],
        ["C7+9", WithChordType(x => x.PlusSeventhMeaning = PlusSeventhMeaning.AugmentedFifth), "C7#9"],

        ["C9+", WithChordType(_ => { }), "C7#9"],
        ["C-9", WithChordType(_ => { }), "C7b9"],
        ["C-7", WithChordType(_ => { }), "Cm7"],

        ["C9", WithChordType(_ => { }), "C9"],
        ["C9", WithChordType(x => x.BareNinthMeaning = BareNinthMeaning.Added), "Cadd9"],
        ["Cm9", WithChordType(x => x.BareNinthMeaning = BareNinthMeaning.Added), "Cm9"],
        ["C7/9", WithChordType(x => x.BareNinthMeaning = BareNinthMeaning.Added), "C9"],

        ["C2", WithChordType(_ => { }), "Csus2"],
        ["C2", WithChordType(x => x.BareSecondMeaning = BareSecondMeaning.Added), "Cadd9"],
        ["Cm2", WithChordType(_ => { }), "Cmadd9"],

        ["C4", WithChordType(_ => { }), "Csus4"],
        ["C4", WithChordType(x => x.BareFourthMeaning = BareFourthMeaning.Added), "Cadd11"],
        ["Cm4", WithChordType(_ => { }), "Cmadd11"],

        ["Cmaj", WithChordType(_ => { }), "Cmaj7"],
        ["Cmaj", WithChordType(x => x.MajWithoutDegreeMeaning = MajWithoutDegreeMeaning.Major), "C"],
        ["Cmaj5-", WithChordType(_ => { }), "Cmaj7b5"],
        ["Cmaj7", WithChordType(x => x.MajWithoutDegreeMeaning = MajWithoutDegreeMeaning.Major), "Cmaj7"],

        ["CM", WithChordType(_ => { }), "Cmaj7"],
        ["CM", WithChordType(x => x.CapitalMWithoutDegreeMeaning = CapitalMWithoutDegreeMeaning.Major), "C"],
        ["CM", WithChordType(x => x.CapitalMWithoutDegreeMeaning = CapitalMWithoutDegreeMeaning.Minor), "Cm"],
        ["CM7", WithChordType(x => x.CapitalMWithoutDegreeMeaning = CapitalMWithoutDegreeMeaning.Minor), "Cmaj7"],

        ["Cdim", WithChordType(_ => { }), "Cdim"],
        ["Cdim", WithChordType(x => x.DimWithoutSeventhMeaning = DimWithoutSeventhMeaning.Seventh), "Cdim7"],

        ["Fm#7", WithChordType(x => x.ForgiveMisplacedRootAccidental = true), "F#m7"],
        ["C7/9b", WithChordType(x => x.ForgiveMisplacedRootAccidental = true), "C7b9"],
        ["Am.", WithChordType(x => x.IgnoreTrailingPunctuation = true), "Am"],
        ["Dm7(4", WithChordType(x => x.ForgiveUnclosedParentheses = true), "Dm7add11"],
    ];

    [Theory]
    [MemberData(nameof(OptionsData))]
    public void Options(string input, ChordParsingOptions options, string expected)
    {
        var result = ChordParser.Parse(input, options);
        Assert.Equal(ChordParseResultType.Success, result.Type);
        Assert.Equal(expected, result.Chord!.ToCanonical());
    }

    [Fact]
    public void Solfege()
    {
        Assert.Equal("C", ChordParser.Parse("Do").Chord!.ToCanonical());
        Assert.Equal("Ddim7", ChordParser.Parse("Do", new() { NoteParsingOptions = new() { Solfege = NotationUse.Never } }).Chord!.ToCanonical());
        Assert.Equal("Bbm", ChordParser.Parse("Sibm").Chord!.ToCanonical());
        Assert.Equal("Ab", ChordParser.Parse("LAb").Chord!.ToCanonical());
        Assert.Equal("F#m7", ChordParser.Parse("Fa#m7").Chord!.ToCanonical());
        Assert.Equal("Am/C", ChordParser.Parse("LAm/DO").Chord!.ToCanonical());
    }

    [Fact]
    public void GermanB()
    {
        var options = new ChordParsingOptions { NoteParsingOptions = new() { HHandling = HHandling.BbMeansH } };
        Assert.Equal("Bb", ChordParser.Parse("B", options).Chord!.ToCanonical());
        Assert.Equal("Bm", ChordParser.Parse("Hm", options).Chord!.ToCanonical());
        Assert.Equal("Bb7", ChordParser.Parse("Bb7", options).Chord!.ToCanonical());
        Assert.Equal("F/Bb", ChordParser.Parse("F/B", options).Chord!.ToCanonical());
        Assert.Equal("B", ChordParser.Parse("B").Chord!.ToCanonical());
        Assert.Equal("Bm", ChordParser.Parse("Hm").Chord!.ToCanonical());
        Assert.Equal(ChordParseError.UnreadableRoot, ChordParser.Parse("Hm", new() { NoteParsingOptions = new() { HHandling = HHandling.HProhibited } }).Error);
    }

    public static readonly TheoryData<string, string> GermanNamesData = new()
    {
        { "fis", "F#m" },
        { "Fism", "F#m" },
        { "cis", "C#m" },
        { "h", "Bm" },
        { "Fis", "F#" },
        { "Es", "Eb" },
        { "As7", "Ab7" },
        { "Des", "C#" },
        { "Esus4", "Esus4" },
        { "a7", "Am7" },
        { "B", "Bb" },
        { "H7", "B7" },
    };

    [Theory]
    [MemberData(nameof(GermanNamesData))]
    public void GermanNames(string input, string expected)
    {
        var options = new ChordParsingOptions { NoteParsingOptions = new() { GermanNoteNames = NotationUse.Always, HHandling = HHandling.BbMeansH } };
        Assert.Equal(expected, ChordParser.Parse(input, options).Chord!.ToCanonical());
    }

    [Fact]
    public void CyrillicLetters()
    {
        var options = new ChordParsingOptions { NoteParsingOptions = new() { ForgiveCyrillicLetters = true } };
        Assert.Equal("C#m", ChordParser.Parse("С#m", options).Chord!.ToCanonical());
        Assert.Equal("Am", ChordParser.Parse("Ам", options).Chord!.ToCanonical());
        Assert.Equal("Em", ChordParser.Parse("Еm", options).Chord!.ToCanonical());
        Assert.Equal("B7", ChordParser.Parse("Н7", options).Chord!.ToCanonical());
        Assert.Equal(ChordParseError.UnreadableRoot, ChordParser.Parse("Аm").Error);
    }

    [Fact]
    public void DetectNeedsTheSong()
    {
        Assert.Throws<InvalidOperationException>(() => ChordParser.Parse("C7+", new() { ChordTypeParsingOptions = new() { PlusSeventhMeaning = PlusSeventhMeaning.Detect } }));
        Assert.Throws<InvalidOperationException>(() => ChordParser.Parse("B", new() { NoteParsingOptions = new() { HHandling = HHandling.Detect } }));
    }
}
