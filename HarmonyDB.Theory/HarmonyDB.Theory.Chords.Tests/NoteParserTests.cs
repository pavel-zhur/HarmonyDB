using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;

namespace HarmonyDB.Theory.Chords.Tests;

public class NoteParserTests
{
    public static readonly TheoryData<string, string, int> PrefixNoteData = new()
    {
        { "C", "C", 1 },
        { "C#m", "C#", 2 },
        { "Bbm7", "Bb", 2 },
        { "F##", "G", 3 },
        { "Ebb", "D", 3 },
        { "A#b#", "Bb", 2 },
        { "Ab#", "Ab", 2 },
        { "LA", "A", 2 },
        { "Si", "B", 2 },
        { "DO#m", "C#", 3 },
        { "Mib", "Eb", 3 },
        { "SOL7", "G", 3 },
        { "Fa#", "F#", 3 },
        { "RE", "D", 2 },
    };

    [Theory]
    [MemberData(nameof(PrefixNoteData))]
    public void PrefixNote(string input, string expected, int expectedLength)
    {
        var length = NoteParser.TryParsePrefixNote(input, out var note, out var representation);
        Assert.Equal(expectedLength, length);
        Assert.Equal(expected, NoteConstants.CanonicalNames[note.Value]);
        Assert.Equal(note, representation.Note);
    }

    [Fact]
    public void Solfege()
    {
        Assert.Equal(0, NoteParser.TryParsePrefixNote("la", out _));
        Assert.Equal(2, NoteParser.TryParsePrefixNote("la", out _, NoteParsingOptions.MostForgiving));
        Assert.Equal(0, NoteParser.TryParsePrefixNote("LA", out _, new NoteParsingOptions { Solfege = NotationUse.Never }));
        Assert.Equal(1, NoteParser.TryParsePrefixNote("DO", out var d, new NoteParsingOptions { Solfege = NotationUse.Never }));
        Assert.Equal("D", NoteConstants.CanonicalNames[d.Value]);
    }

    [Fact]
    public void GermanB()
    {
        var german = new NoteParsingOptions { HHandling = HHandling.BbMeansH };
        Assert.Equal("Bb", NoteConstants.CanonicalNames[NoteParser.ParseNote("B", german).Value]);
        Assert.Equal("B", NoteConstants.CanonicalNames[NoteParser.ParseNote("H", german).Value]);
        Assert.Equal("Bb", NoteConstants.CanonicalNames[NoteParser.ParseNote("Bb", german).Value]);
        Assert.Equal("B", NoteConstants.CanonicalNames[NoteParser.ParseNote("B").Value]);
        Assert.False(NoteParser.TryParseNote("H", out _, new NoteParsingOptions { HHandling = HHandling.HProhibited }));
    }

    [Fact]
    public void ParseNoteUsesOptions()
    {
        Assert.Equal("C", NoteConstants.CanonicalNames[NoteParser.ParseNote("c", NoteParsingOptions.MostForgiving).Value]);
        Assert.Throws<ArgumentOutOfRangeException>(() => NoteParser.ParseNote("c"));
    }
}
