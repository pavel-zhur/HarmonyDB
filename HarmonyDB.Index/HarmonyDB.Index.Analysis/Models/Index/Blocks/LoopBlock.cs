using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class LoopBlock : LoopBlockBase
{
    public override BlockType Type => BlockType.Loop;
}