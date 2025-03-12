using HarmonyDB.Theory.Chords.Constants;

namespace HarmonyDB.Theory.Chords.Tests;

public class ChordConstantsTests
{
    [Fact]
    public void AllRepresentationsUnique()
    {
        Assert.Distinct(ChordConstants.AllRepresentations.Select(x => x.representation));
    }
}