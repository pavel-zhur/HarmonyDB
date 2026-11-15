using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;
using Xunit.Abstractions;

namespace HarmonyDB.Theory.Chords.Tests;

public class AllChordsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void NoExceptions()
    {
        var results = Resources.AllChords
            .Select(x => (chord: x.Key, count: x.Value, result: ChordParser.Parse(x.Key, ChordParsingOptions.MostForgiving)))
            .ToList();

        var total = results.Sum(x => (long)x.count);
        foreach (var group in results.GroupBy(x => x.result.Error?.ToString() ?? x.result.Type.ToString()).OrderByDescending(g => g.Sum(x => (long)x.count)))
        {
            var count = group.Sum(x => (long)x.count);
            testOutputHelper.WriteLine($"{group.Key}: {count} occurrences ({100.0 * count / total:0.000}%), {group.Count()} distinct; {string.Join(" ", group.OrderByDescending(x => x.count).Take(10).Select(x => x.chord))}");
        }
    }

    /// <summary>
    /// The canonical name of every parsed chord parses with the default options to the same chord.
    /// </summary>
    [Fact]
    public void CanonicalRoundTrip()
    {
        foreach (var chord in Resources.AllChords.Keys)
        {
            var parsed = ChordParser.Parse(chord, ChordParsingOptions.MostForgiving).Chord;
            if (parsed == null)
                continue;

            var canonical = parsed.ToCanonical();
            var reparsed = ChordParser.Parse(canonical);
            Assert.True(reparsed.Type == ChordParseResultType.Success, $"{chord} -> {canonical}: {reparsed.Error}");
            Assert.Equal(canonical, reparsed.Chord!.ToCanonical());
            Assert.Equal(parsed.ChordType, reparsed.Chord.ChordType);
            Assert.Equal(parsed.Root, reparsed.Chord.Root);
            Assert.Equal(parsed.Bass, reparsed.Chord.Bass);
        }
    }
}
