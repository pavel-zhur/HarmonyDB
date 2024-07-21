namespace HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;

public interface IIndexedBlock : IBlock
{
    IEnumerable<IIndexedBlock> Children { get; }
}