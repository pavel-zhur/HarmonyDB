using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.CompactV1;

public class CompactChordsProgression : ISearchableChordsProgression
{
    public required int HarmonySequenceLength { get; init; }
    
    public required IReadOnlyList<CompactHarmonyMovementsSequence> ExtendedHarmonyMovementsSequences { get; init; }

    IReadOnlyList<ISearchableHarmonyMovementsSequence> ISearchableChordsProgression.ExtendedHarmonyMovementsSequences => ExtendedHarmonyMovementsSequences;

    public static CompactChordsProgression Deserialize(BinaryData data)
    {
        using var stream = data.ToStream();
        using var reader = new BinaryReader(stream);
        return new()
        {
            HarmonySequenceLength = reader.ReadInt32(),
            ExtendedHarmonyMovementsSequences = Enumerable.Repeat(0, reader.ReadInt32())
                .Select(_ => new CompactHarmonyMovementsSequence
                {
                    FirstMovementFromIndex = reader.ReadInt32(),
                    FirstRoot = reader.ReadSByte(),
                    Movements = Enumerable.Repeat(0, reader.ReadInt32())
                        .Select(_ => new CompactHarmonyMovement
                        {
                            RootDelta = reader.ReadSByte(),
                            FromType = (ChordType)reader.ReadByte(),
                            ToType = (ChordType)reader.ReadByte(),
                        })
                        .ToArray(),
                })
                .ToList(),
        };
    }

    public BinaryData Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(HarmonySequenceLength);
        writer.Write(ExtendedHarmonyMovementsSequences.Count);
        foreach (var extendedHarmonyMovementsSequence in ExtendedHarmonyMovementsSequences)
        {
            writer.Write(extendedHarmonyMovementsSequence.FirstMovementFromIndex);
            writer.Write((sbyte)extendedHarmonyMovementsSequence.FirstRoot);
            writer.Write(extendedHarmonyMovementsSequence.Movements.Length);
            foreach (var compactHarmonyMovement in extendedHarmonyMovementsSequence.Movements.Span)
            {
                writer.Write((sbyte)compactHarmonyMovement.RootDelta);
                writer.Write((byte)compactHarmonyMovement.FromType);
                writer.Write((byte)compactHarmonyMovement.ToType);
            }
        }

        return new(stream.ToArray());
    }
}