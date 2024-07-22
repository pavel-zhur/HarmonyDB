using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;

public interface IIndexedBlock : IBlock
{
    IEnumerable<IIndexedBlock> Children { get; }

    string? GetNormalizedCoordinate(int index);
    
    IndexedBlockType Type { get; }
    
    string Normalized { get; }

    float Score => 1;
    
    byte NormalizationRoot { get; }
}