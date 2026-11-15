using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;

namespace HarmonyDB.Theory.Chords.Tests;

public class SongChordsTests
{
    private static readonly ChordParsingOptions Detecting = new() { DetectHHandlingFromSong = true };

    public static readonly TheoryData<string, HHandling> DetectData = new()
    {
        // B flat written as B in F major and D minor.
        { "F C Dm Gm B C7", HHandling.BbMeansH },
        { "Dm Gm A7 B C F", HHandling.BbMeansH },

        // B natural where it belongs: the dominant of E minor, a diatonic chord, a secondary dominant.
        { "G C D Em B7", HHandling.BMeansH },
        { "E A B C#m F#m", HHandling.BMeansH },
        { "C Am Dm7 G7 E7 B7 Em", HHandling.BMeansH },

        // H is used: the German notation.
        { "Am Dm E H7", HHandling.BbMeansH },

        // B flat is spelled explicitly: the bare B is B natural.
        { "F C Dm Bb B", HHandling.BMeansH },

        // Too few other chords to tell the key.
        { "F B", HHandling.BMeansH },
    };

    [Theory]
    [MemberData(nameof(DetectData))]
    public void Detect(string chords, HHandling expected)
    {
        Assert.Equal(expected, ChordParser.DetectHHandling(chords.Split(' ')));
    }

    [Fact]
    public void DetectFromGerman()
    {
        // The options say German, but the bare B is plainly B natural here.
        var german = new ChordParsingOptions { NoteParsingOptions = new() { HHandling = HHandling.BbMeansH } };
        Assert.Equal(HHandling.BMeansH, ChordParser.DetectHHandling("E A B C#m F#m G#m".Split(' '), german));
    }

    [Fact]
    public void ParseWithSong()
    {
        var song = "F C Dm Gm B C7".Split(' ');
        Assert.Equal("Bb", ChordParser.Parse("B", song, Detecting).Chord!.ToCanonical());
        Assert.Equal("B", ChordParser.Parse("B", song).Chord!.ToCanonical());

        var all = ChordParser.ParseSong(song, Detecting);
        Assert.Equal("Bb", all["B"].Chord!.ToCanonical());
        Assert.Equal("F", all["F"].Chord!.ToCanonical());
    }
}
