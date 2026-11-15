using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;

namespace HarmonyDB.Index.Analysis.Tests;

public class SongLoopsSearchTests(SongLoopsSearch songLoopsSearch)
{
    [Fact]
    public void FindLoopsAndShowOne()
    {
        // Am F C G Am F C G Dm E: the loop Am F C G twice, then a tail outside it.
        var (loops, progression) = songLoopsSearch.FindLoops(
        [
            "[!0!]m", "[!8!]", "[!3!]", "[!10!]",
            "[!0!]m", "[!8!]", "[!3!]", "[!10!]",
            "[!5!]m", "[!7!]",
        ]);

        Assert.Equal(10, progression.OriginalSequence.Count);
        var loop = Assert.Single(loops);
        Assert.Equal(4, loop.Normalized.DeserializeLoop().Length);

        var attributes = songLoopsSearch.BuildCustomAttributesForLoop(progression, loop);

        // the loop covers the first eight chords, not the tail.
        Assert.Equal(Enumerable.Range(0, 8), attributes.Keys.Order());

        // the loop's beginning is marked once in each of its two turns.
        Assert.Equal(2, attributes.Values.Count(x => x == ProgressionsVisualizer.AttributeSearchFirst));
        Assert.All(attributes.Values, x => Assert.Contains(x, new[] { ProgressionsVisualizer.AttributeSearch, ProgressionsVisualizer.AttributeSearchFirst }));
    }
}
