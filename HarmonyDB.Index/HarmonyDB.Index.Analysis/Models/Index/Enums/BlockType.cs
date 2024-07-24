namespace HarmonyDB.Index.Analysis.Models.Index.Enums;

public enum BlockType : byte
{
    Loop,
    Sequence,

    PingPong,
    SequenceStart,
    SequenceEnd,
    
    MassiveOverlaps,
    LoopSelfJump,
    LoopSelfMultiJump,
    
    PolySequence,
    PolyLoop,
}