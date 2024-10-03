using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class ChordParserTests(ITestOutputHelper testOutputHelper)
{
    public static readonly TheoryData<string, List<(ChordType? type, ChordTypeExtension? extension, ChordTypeAdditions? addition, byte? fret, ChordTypeMeaninglessAddition? meaninglessAddition, bool fromParentheses, MatchAmbiguity matchAmbiguity)>?> ParseChordTypeData = new()
    {
        { "6", [(null, null, ChordTypeAdditions.Add6, null, null, false, MatchAmbiguity.Safe)] },
        { "7", [(null, ChordTypeExtension.X7, null, null, null, false, MatchAmbiguity.Safe)] },
    };

    [Theory]
    [MemberData(nameof(ParseChordTypeData))]
    public void ParseChordType(string input, List<(ChordType? type, ChordTypeExtension? extension, ChordTypeAdditions? addition, byte? fret, ChordTypeMeaninglessAddition? meaninglessAddition, bool fromParentheses, MatchAmbiguity matchAmbiguity)>? expected)
    {
        var actual = ChordParser.TryGetTokens(input, ChordTypeParsingOptions.MostForgiving);
        Assert.Equal(expected, actual);
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
        var actual = ChordParser.UnwrapParentheses(input, ChordTypeParsingOptions.MostForgiving);
        Assert.Equal(expected, actual);
    }
}