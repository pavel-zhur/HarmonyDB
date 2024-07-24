using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class PolyLoopBlock : LoopBlockBase, IPolyBlock
{
    public override BlockType Type => BlockType.PolyLoop;
    
    public bool SelfOverlapsDetected { get; set; }
}