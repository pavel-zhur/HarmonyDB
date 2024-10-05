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
}