using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;

namespace HarmonyDB.Theory.Chords.Tests;

public class SongChordsTests
{
    private static ChordParsingOptions Detecting(Action<ChordParsingOptions>? configure = null)
    {
        var options = new ChordParsingOptions
        {
            NoteParsingOptions = new() { HHandling = HHandling.Detect, Solfege = NotationUse.Detect, GermanNoteNames = NotationUse.Detect },
            ChordTypeParsingOptions = new() { PlusSeventhMeaning = PlusSeventhMeaning.Detect, BareNinthMeaning = BareNinthMeaning.Detect, IgnoreVariantNumbers = true },
        };

        configure?.Invoke(options);
        return options;
    }

    public static readonly TheoryData<string, HHandling> DetectData = new()
    {
        // B flat written as B, in F major and D minor: the B chords are out of the key and their neighbours share B flat.
        { "F Dm B C F Dm B C F", HHandling.BbMeansH },
        { "Dm Gm B A Dm Gm B A Dm", HHandling.BbMeansH },
        { "Dm Gm A7 B Gm A7 Dm", HHandling.BbMeansH },

        // B natural where it belongs: the dominant of E minor, a diatonic chord, a secondary dominant.
        { "G C D Em B7 Em C D G", HHandling.BMeansH },
        { "E A B C#m F#m B E", HHandling.BMeansH },
        { "C Am Dm7 G7 E7 Am B7 Em", HHandling.BMeansH },

        // A song that modulates: no ground to call its B wrong.
        { "A D G C Bm C#m A", HHandling.BMeansH },

        // H is used: the German notation.
        { "Am Dm E H7 Am", HHandling.BbMeansH },

        // B flat is spelled explicitly: the bare B is B natural.
        { "F C Dm Bb B C", HHandling.BMeansH },

        // Too few other chords to tell the key.
        { "F B F", HHandling.BMeansH },
    };

    [Theory]
    [MemberData(nameof(DetectData))]
    public void DetectB(string chords, HHandling expected)
    {
        Assert.Equal(expected, ChordParser.DetectHHandling(chords.Split(' ')));
    }

    [Fact]
    public void ParseSong()
    {
        var song = "F Dm B C F Dm B C F".Split(' ');
        Assert.Equal("Bb", ChordParser.Parse("B", song, Detecting()).Chord!.ToCanonical());
        Assert.Equal("B", ChordParser.Parse("B", song).Chord!.ToCanonical());

        var all = ChordParser.ParseSong(song, Detecting());
        Assert.Equal("Bb", all["B"].Chord!.ToCanonical());
        Assert.Equal("Dm", all["Dm"].Chord!.ToCanonical());
    }

    [Fact]
    public void Solfege()
    {
        // "Do" is C in a song written in solfège, and D diminished in a song written in letters.
        Assert.Equal("C", ChordParser.ParseSong("Do Sol LAm Fa".Split(' '), Detecting())["Do"].Chord!.ToCanonical());
        Assert.Equal("Ddim7", ChordParser.ParseSong("C Do Dm G7".Split(' '), Detecting())["Do"].Chord!.ToCanonical());
    }

    [Fact]
    public void GermanNames()
    {
        var song = ChordParser.ParseSong("A fis h E A fis B Cis".Split(' '), Detecting());
        Assert.Equal("F#m", song["fis"].Chord!.ToCanonical());
        Assert.Equal("Bm", song["h"].Chord!.ToCanonical());
        Assert.Equal("Bb", song["B"].Chord!.ToCanonical());

        // Without German names, lower case stays unread (case sensitive) and B is B.
        var english = ChordParser.ParseSong("A Bm E A B".Split(' '), Detecting());
        Assert.Equal("B", english["B"].Chord!.ToCanonical());
    }

    public static readonly TheoryData<string, string, string> PlusSeventhData = new()
    {
        // The usual: the major seventh.
        { "D7+ Em F#m G7+ D7+", "G7+", "Gmaj7" },

        // The song spells the major seventh otherwise: the plus is the augmented fifth.
        { "Am Fmaj7 E7+ Am", "E7+", "E7#5" },

        // The plus right after the plain seventh: the rising fifth.
        { "D A7 A7+ D", "A7+", "A7#5" },

        // The plus before the plain seventh: the falling line of the major seventh.
        { "F F7+ F7 Bb", "F7+", "Fmaj7" },
    };

    [Theory]
    [MemberData(nameof(PlusSeventhData))]
    public void PlusSeventh(string chords, string chord, string expected)
    {
        Assert.Equal(expected, ChordParser.ParseSong(chords.Split(' '), Detecting())[chord].Chord!.ToCanonical());
    }

    [Fact]
    public void BareNinth()
    {
        // C9 in G major: its B flat is out of the key — added. C9 resolving to F: its B flat is in the key — dominant.
        Assert.Equal("Cadd9", ChordParser.ParseSong("G D Em C9 G D".Split(' '), Detecting())["C9"].Chord!.ToCanonical());
        Assert.Equal("C9", ChordParser.ParseSong("F Bb Gm C9 F Dm".Split(' '), Detecting())["C9"].Chord!.ToCanonical());
    }

    [Fact]
    public void Variants()
    {
        var song = ChordParser.ParseSong("G1 G2 G3 C D".Split(' '), Detecting());
        Assert.Equal("G", song["G2"].Chord!.ToCanonical());
        Assert.Equal("G", song["G3"].Chord!.ToCanonical());

        // Without the first variant, "G2" is the suspended second.
        Assert.Equal("Gsus2", ChordParser.ParseSong("G2 C D".Split(' '), Detecting())["G2"].Chord!.ToCanonical());
    }
}
