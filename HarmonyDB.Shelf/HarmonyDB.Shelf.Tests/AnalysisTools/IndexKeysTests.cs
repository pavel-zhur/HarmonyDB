using HarmonyDB.Shelf.Api.Areas.AnalysisTools.Services;

namespace HarmonyDB.Shelf.Tests.AnalysisTools;

/// <summary>
/// The index numbers notes from A (A is 0, C is 3): its keys reach the browser as the chord sheet's keys.
/// </summary>
public sealed class IndexKeysTests
{
    [Theory]
    [InlineData(1, "A", true)]      // A minor: tonic 0, minor
    [InlineData(6, "C", false)]     // C major: tonic 3
    [InlineData(9, "C#", true)]     // C# minor: tonic 4
    [InlineData(8, "Db", false)]    // D-flat major: tonic 4
    [InlineData(18, "F#", false)]
    [InlineData(23, "G#", true)]
    [InlineData(2, "Bb", false)]
    public void AKeyIsNamedAsTheSheetNamesIt(int index, string tonic, bool isMinor)
    {
        var key = IndexKeys.Key(index);
        Assert.Equal((tonic, isMinor), (key.Tonic, key.IsMinor));
    }

    [Fact]
    public void ALoopsKeyIsTheKeyInTheItem()
    {
        // the loop's key relative to its normalized form, which starts in the item from E (7): its tonic 0 there is E
        Assert.Equal("E", IndexKeys.LoopKey(0, 7).Tonic);
        Assert.Equal("E", IndexKeys.NoteName(7));
        Assert.Equal("C", IndexKeys.NoteName(3));
    }
}
