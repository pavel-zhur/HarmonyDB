namespace HarmonyDB.Index.Analysis.Models.Interfaces;

public interface ISearchableHarmonyMovement
{
    int RootDelta { get; }

    ChordType FromType { get; }

    ChordType ToType { get; }
}