using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;

public interface IBlock
{
    int StartIndex { get; }

    int EndIndex { get; }

    int BlockLength { get; }

    BlockType Type { get; }
}