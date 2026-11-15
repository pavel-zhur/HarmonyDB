export enum BlockType {
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

/** BlockType.ToString(): the name, or the number for a value that has no name. */
export function blockTypeToString(type: BlockType): string {
    return BlockType[type] ?? String(type);
}
