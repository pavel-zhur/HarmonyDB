using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class EdgeBlock : IIndexedBlock
{
    public EdgeBlock(BlockType type, int sequenceLength)
    {
        Normalized = type.ToString();
        Type = type;
        
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        (StartIndex, EndIndex) = type switch
        {
            BlockType.SequenceStart => (-1, -1),
            BlockType.SequenceEnd => (sequenceLength, sequenceLength),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public string Normalized { get; }
    
    public byte NormalizationRoot => 0;

    public int StartIndex { get; }

    public int EndIndex { get; }

    public int BlockLength => 1;
    
    public IEnumerable<IIndexedBlock> Children => [];

    public string? GetNormalizedCoordinate(int index) => null; // song start blocks do not have normalization shifts

    public BlockType Type { get; }
}