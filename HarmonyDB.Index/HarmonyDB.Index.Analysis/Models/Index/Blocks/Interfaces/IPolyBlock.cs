namespace HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;

public interface IPolyBlock : IIndexedBlock
{
    /// <summary>
    /// Whether self overlaps were detected among blocks with the same <see cref="IIndexedBlock.Normalized"/> and <see cref="IIndexedBlock.NormalizationRoot"/>.
    /// </summary>
    bool SelfOverlapsDetected { get; set; }
}