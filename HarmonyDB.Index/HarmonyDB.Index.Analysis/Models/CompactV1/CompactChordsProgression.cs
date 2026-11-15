using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

public class CompactChordsProgression : ISearchableChordsProgression
{
    public required int HarmonySequenceLength { get; init; }
    
    public required IReadOnlyList<CompactHarmonyMovementsSequence> ExtendedHarmonyMovementsSequences { get; init; }

    IReadOnlyList<ISearchableHarmonyMovementsSequence> ISearchableChordsProgression.ExtendedHarmonyMovementsSequences => ExtendedHarmonyMovementsSequences;

    public BinaryData Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(HarmonySequenceLength);
        writer.Write(ExtendedHarmonyMovementsSequences.Count);
        foreach (var extendedHarmonyMovementsSequence in ExtendedHarmonyMovementsSequences)
        {
            writer.Write(extendedHarmonyMovementsSequence.FirstMovementFromIndex);
            writer.Write(extendedHarmonyMovementsSequence.FirstRoot);
            writer.Write(extendedHarmonyMovementsSequence.Movements.Length);
            foreach (var compactHarmonyMovement in extendedHarmonyMovementsSequence.Movements.Span)
            {
                writer.Write(compactHarmonyMovement.RootDelta);
                writer.Write((byte)compactHarmonyMovement.FromType);
                writer.Write((byte)compactHarmonyMovement.ToType);
            }
        }

        return new(stream.ToArray());
    }
}