using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class AllChordsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void NoExceptions()
    {
        var success = 0;
        foreach (var (chord, count) in Resources.AllChords)
        {
            ChordParser.Parse(chord, ChordParsingOptions.MostForgiving);
            success += count;
        }

        testOutputHelper.WriteLine($"Success: {success}.");
    }
}