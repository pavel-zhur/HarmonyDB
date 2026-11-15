import { at, atFromEnd } from '../../../../common/dotnet/Linq';
import { BlockType } from '../Enums/BlockType';
import type { IBlock } from './Interfaces/IBlock';
import type { LoopBlock } from './LoopBlock';
import type { LoopSelfJumpBlock } from './LoopSelfJumpBlock';

export class LoopSelfMultiJumpBlock implements IBlock {
    constructor(init: { normalizationRootsFlow: readonly number[]; childLoops: readonly LoopBlock[]; childJumps: readonly LoopSelfJumpBlock[] }) {
        this.normalizationRootsFlow = init.normalizationRootsFlow;
        this.childLoops = init.childLoops;
        this.childJumps = init.childJumps;
    }

    get loopLength(): number {
        return at(this.childLoops, 0).loopLength;
    }

    get normalized(): string {
        return at(this.childLoops, 0).normalized;
    }

    get startIndex(): number {
        return at(this.childLoops, 0).startIndex;
    }

    get endIndex(): number {
        return atFromEnd(this.childLoops, 1).endIndex;
    }

    /**
     * Shows the key(s) the main loop exists in.
     */
    readonly normalizationRootsFlow: readonly number[];

    get hasModulations(): boolean {
        return this.normalizationRootsFlow.length > 1;
    }

    get isModulation(): boolean {
        return at(this.normalizationRootsFlow, 0) !== atFromEnd(this.normalizationRootsFlow, 1);
    }

    readonly childLoops: readonly LoopBlock[];

    readonly childJumps: readonly LoopSelfJumpBlock[];

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    get children(): readonly IBlock[] {
        return this.childJumps;
    }

    // IBlock.Type
    get type(): BlockType {
        return BlockType.LoopSelfMultiJump;
    }
}
