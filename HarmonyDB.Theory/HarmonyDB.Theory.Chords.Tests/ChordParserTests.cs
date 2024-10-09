using HarmonyDB.Theory.Chords.Constants;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class ChordParserTests(ITestOutputHelper testOutputHelper)
{
    public static readonly TheoryData<string, (List<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? result, ChordParseError? error)> ParseChordTypeData = new()
    {
        { "6", ([(new(ChordTypeAdditions.Add6), false, MatchAmbiguity.AlterableInt)], null) },
        { "7", ([(new(ChordTypeExtension.X7), false, MatchAmbiguity.AlterableInt)], null) },
    };

    [Theory]
    [MemberData(nameof(ParseChordTypeData))]
    public void ParseChordType(string input, (List<(ChordTypeToken token, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? result, ChordParseError? error)? expected)
    {
        var actual = ChordParser.TryGetTokens(input, ChordTypeParsingOptions.MostForgiving);
        Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));
        testOutputHelper.WriteLine(JsonConvert.SerializeObject(actual));
    }

    public static readonly IEnumerable<object[]> UnwrapParenthesesData =
    [
        ["abc", (List<(string, bool)>)[("abc", false)]],
        ["ab()c", (List<(string, bool)>)[("ab()c", false)]],
        ["ab())", (List<(string, bool)>)[("ab())", false)]],
        ["ab(()", (List<(string, bool)>)[("ab(()", false)]],
        ["ab()()", (List<(string, bool)>)[("ab()()", false)]],
        ["ab(cd)", (List<(string, bool)>)[("ab", false), ("cd", true)]],
        ["ab(c,d)", (List<(string, bool)>)[("ab", false), ("c", true), ("d", true)]],
        ["ab(c,d,)", (List<(string, bool)>)[("ab", false), ("c", true), ("d", true)]],
        ["ab(c,,d)", (List<(string, bool)>)[("ab", false), ("c", true), ("d", true)]],
        ["(c,,d)", (List<(string, bool)>)[("c", true), ("d", true)]],
    ];

    [Theory]
    [MemberData(nameof(UnwrapParenthesesData))]
    public void UnwrapParentheses(string input, IReadOnlyList<(string fragment, bool fromParentheses)> expected)
    {
        var (actual, error) = ChordParser.TryUnwrapParentheses(input, ChordTypeParsingOptions.MostForgiving);
        Assert.Equal(expected, actual);
    }

    public static readonly TheoryData<string, (string, NoteRepresentation?, byte?, Note?)> ExtractBassAndFretData = new()
    {
        { "X", ("X", null, null, null) },
        { "/A", ("", new(NaturalNoteRepresentation.A, 0, 0), null, new((byte)NaturalNoteRepresentation.A)) },
        { "/A(III)", ("", new(NaturalNoteRepresentation.A, 0, 0), 3, new((byte)NaturalNoteRepresentation.A)) },
        { "X234\\F\\A(III)", ("X234\\F", new(NaturalNoteRepresentation.A, 0, 0), 3, new((byte)NaturalNoteRepresentation.A)) },
    };

    [Theory]
    [MemberData(nameof(ExtractBassAndFretData))]
    public void ExtractBassAndFret(string input, (string modified, NoteRepresentation? bass, byte? fret, Note? bassNote) expected)
    {
        var actual = ChordParser.ExtractBassAndFret(ref input, out var bassNote, ChordParsingOptions.MostForgiving);
        Assert.Equal(bassNote.HasValue, actual.bass != null);
        Assert.Equal(expected, (input, actual.bass, actual.fret, bassNote));
    }
}