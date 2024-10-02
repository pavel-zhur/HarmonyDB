using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class AllChordsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void LittleChordsFailed()
    {
        var success = 0;
        var failure = 0;
        foreach (var (chord, count) in Resources.AllChords)
        {
            try
            {
                ChordParser.Parse(chord, ChordParsingOptions.MostForgiving);
                success += count;
            }
            catch
            {
                failure += count;
                testOutputHelper.WriteLine($"{count}\t{chord}");
            }
        }

        testOutputHelper.WriteLine($"Success: {success}, failure: {failure}.");
    }
}