using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

[Obsolete]
public class PolyLoopBlock : LoopBlockBase
{
    public override BlockType Type => BlockType.PolyLoop;
}