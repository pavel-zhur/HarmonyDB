namespace HarmonyDB.Index.Analysis.Models.Interfaces;

public interface ISearchableHarmonyMovement
{
    byte RootDelta { get; }

    ChordType FromType { get; }

    ChordType ToType { get; }
}