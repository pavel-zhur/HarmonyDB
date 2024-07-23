namespace HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;

public interface IIndexedBlock : IBlock
{
    IEnumerable<IIndexedBlock> Children { get; }

    string? GetNormalizedCoordinate(int index);
    
    string Normalized { get; }

    float Score => 1;
    
    byte NormalizationRoot { get; }
}