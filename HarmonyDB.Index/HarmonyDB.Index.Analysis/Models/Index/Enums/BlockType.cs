namespace HarmonyDB.Index.Analysis.Models.Index.Enums;

public enum BlockType : byte
{
    Loop,
    Sequence,

    PingPong,
    RoundRobin,
    
    SequenceStart,
    SequenceEnd,
    
    MassiveOverlaps,
    LoopSelfJump,
    LoopSelfMultiJump,
    
    PolySequence,
    PolyLoop,

    RoundRobinLx,
}