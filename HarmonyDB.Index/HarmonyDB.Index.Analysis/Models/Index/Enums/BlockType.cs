namespace HarmonyDB.Index.Analysis.Models.Index.Enums;

public enum BlockType : byte
{
    Loop,
    Sequence,

    [Obsolete]
    PolyLoop,
    PingPong,
    SequenceStart,
    SequenceEnd,
    
    MassiveOverlaps,
    LoopSelfJump,
    LoopSelfMultiJump,
}