namespace HarmonyDB.Common.Fingerings.Models;

public class FingeringsModelIndex
{
    public FingeringsModel Model { get; init; }

    public IReadOnlyDictionary<string, FingeringsChordType> TypesByChordText { get; init; }
}
