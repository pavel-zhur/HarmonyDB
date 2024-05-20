using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.V1;

public class CompressedChordProgressionDataV1
{
    public required List<int> Sequence { get; set; }

    public required List<ChordProgressionDataV1> Distinct { get; set; }

    public static CompressedChordProgressionDataV1 Compress(IReadOnlyList<ChordProgressionDataV1> sequence)
    {
        var distinct = sequence.Distinct().ToList();
        var indices = distinct.WithIndices().ToDictionary(x => x.x, x => x.i);
        return new()
        {
            Distinct = distinct,
            Sequence = sequence.Select(x => indices[x]).ToList(),
        };
    }

    public List<ChordProgressionDataV1> Decompress() => Sequence.Select(x => Distinct[x]).ToList();
}