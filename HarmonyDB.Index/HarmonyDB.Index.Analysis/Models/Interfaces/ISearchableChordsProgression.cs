namespace HarmonyDB.Index.Analysis.Models.Interfaces;

public interface ISearchableChordsProgression
{
    int HarmonySequenceLength { get; }

    IReadOnlyList<ISearchableHarmonyMovementsSequence> ExtendedHarmonyMovementsSequences { get; }
}