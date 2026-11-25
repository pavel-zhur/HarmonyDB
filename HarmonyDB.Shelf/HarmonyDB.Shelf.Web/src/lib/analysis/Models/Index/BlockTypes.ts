import { BlockType } from './Enums/BlockType';

/** Enum.GetValues<BlockType>(): the values in ascending order. */
const allBlockTypes: readonly BlockType[] = Object.values(BlockType)
    .filter((x): x is BlockType => typeof x === 'number')
    .sort((a, b) => a - b);

export const BlockTypes = {
    Loop: [BlockType.Loop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd] as readonly BlockType[],

    LoopAndJumps: [BlockType.Loop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd, BlockType.LoopSelfJump, BlockType.LoopSelfMultiJump] as readonly BlockType[],

    All: allBlockTypes,

    AllNoPingPongs: allBlockTypes.filter(x => x !== BlockType.PingPong) as readonly BlockType[],

    PolySequences: [BlockType.PolySequence, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd] as readonly BlockType[],

    LoopsAndPolySequences: [BlockType.Loop, BlockType.PolySequence, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd] as readonly BlockType[],

    LoopsAndPolyLoops: [BlockType.Loop, BlockType.PolyLoop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd] as readonly BlockType[],
} as const;
