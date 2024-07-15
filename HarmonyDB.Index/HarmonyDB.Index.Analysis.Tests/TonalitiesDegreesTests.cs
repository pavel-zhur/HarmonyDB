using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Tests;

public class TonalitiesDegreesTests(InputParser inputParser, ILogger<TonalitiesDegreesTests> logger)
{
    [Fact]
    public void All()
    {
        var combinations = Enumerable.Range(0, 12).SelectMany(r =>
                Enum.GetValues<ChordType>().SelectMany(c =>
                    Enumerable.Range(0, 12).SelectMany(t =>
                        Enumerable.Range(0, 2).Select(m => (
                            chord: $"{new Note((byte)r).Representation(new())}{c.ChordTypeToString()}",
                            tonality: $"{new Note((byte)t).Representation(new())}{(m == 1 ? "m" : string.Empty)}")))))
            .ToList();

        logger.LogInformation(string.Join(Environment.NewLine, combinations
            .Select(c => $"{c} -> {inputParser
                .ParseChord(c.chord).HarmonyData
                !.SelectSingle(x => (x.Root, x.ChordType))
                .ToChord(c.tonality.TryParseBestTonality()!.Value)}")));
    }

    // Ⅰ, Ⅱ, Ⅲ, Ⅳ, Ⅴ, Ⅵ, Ⅶ
    // ⅰ, ⅱ, ⅲ, ⅳ, ⅴ, ⅵ, ⅶ
    [InlineData("Am", "Am", "ⅰ")]
    [InlineData("C", "Am", "Ⅲ")]
    [InlineData("Cm", "Am", "ⅲ")]
    [InlineData("Bbdim", "Am", "♭ⅱdim")]
    [Theory]
    public void Test(string chord, string tonality, string expected)
    {
        Assert.Equal(
            expected,
            inputParser
                .ParseChord(chord).HarmonyData
                !.SelectSingle(x => (x.Root, x.ChordType))
                .ToChord(tonality.TryParseBestTonality()!.Value));
    }
}