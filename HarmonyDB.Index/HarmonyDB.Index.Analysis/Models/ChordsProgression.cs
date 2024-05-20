using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Interfaces;
using HarmonyDB.Index.Analysis.Models.V1;

namespace HarmonyDB.Index.Analysis.Models;

public class ChordsProgression : ISearchableChordsProgression
{
    public required IReadOnlyList<(ChordProgressionDataV1 original, HarmonyGroup harmonyGroup, int indexInHarmonyGroup, HarmonyMovementsSequence? standardMovementsSequence, HarmonyMovementsSequence? extendedMovementsSequence)> OriginalSequence { get; init; }

    public required IReadOnlyList<(HarmonyGroup harmonyGroup, HarmonyMovementsSequence? standardMovementsSequence, HarmonyMovementsSequence? extendedMovementsSequence)> HarmonySequence { get; init; }

    public required IReadOnlyList<HarmonyMovementsSequence>? StandardHarmonyMovementsSequences { get; init; }
    
    public required IReadOnlyList<HarmonyMovementsSequence> ExtendedHarmonyMovementsSequences { get; init; }

    int ISearchableChordsProgression.HarmonySequenceLength => HarmonySequence.Count;

    IReadOnlyList<ISearchableHarmonyMovementsSequence> ISearchableChordsProgression.ExtendedHarmonyMovementsSequences => ExtendedHarmonyMovementsSequences;

    public CompactChordsProgression Compact()
        => new()
        {
            HarmonySequenceLength = HarmonySequence.Count,
            ExtendedHarmonyMovementsSequences = ExtendedHarmonyMovementsSequences.Select(x => x.Compact()).ToList(),
        };
}