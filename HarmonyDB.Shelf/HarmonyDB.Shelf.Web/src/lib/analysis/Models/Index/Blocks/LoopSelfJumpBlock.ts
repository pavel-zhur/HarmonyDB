import { intMod } from '../../../../common/dotnet/Numbers';
import { Note } from '../../../../representations';
import type { CompactHarmonyMovement } from '../../CompactV1/CompactHarmonyMovement';
import { BlockType } from '../Enums/BlockType';
import { LoopSelfJumpType } from '../Enums/LoopSelfJumpType';
import type { IBlock } from './Interfaces/IBlock';
import type { LoopBlock } from './LoopBlock';

/**
 * The C# class implements IBlock.Type (a BlockType) explicitly and has a public Type of LoopSelfJumpType; TypeScript
 * has one member per name, so here `type` is IBlock.Type and `loopSelfJumpType` is the public Type.
 */
export class LoopSelfJumpBlock implements IBlock {
    constructor(init: { loop1: LoopBlock; loop2: LoopBlock; jointLoop: LoopBlock | null; jointMovement: CompactHarmonyMovement | null }) {
        this.loop1 = init.loop1;
        this.loop2 = init.loop2;
        this.jointLoop = init.jointLoop;
        this.jointMovement = init.jointMovement;
    }

    get loopLength(): number {
        return this.loop1.loopLength;
    }

    get normalized(): string {
        return this.loop1.normalized;
    }

    get startIndex(): number {
        return this.loop1.startIndex;
    }

    get endIndex(): number {
        return this.loop2.endIndex;
    }

    get isModulation(): boolean {
        return this.loop1.normalizationRoot !== this.loop2.normalizationRoot;
    }

    readonly loop1: LoopBlock;

    readonly loop2: LoopBlock;

    readonly jointLoop: LoopBlock | null;

    jointMovement: CompactHarmonyMovement | null;

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    get children(): readonly IBlock[] {
        return this.jointLoop === null ? [this.loop1, this.loop2] : [this.loop1, this.jointLoop, this.loop2];
    }

    // IBlock.Type
    get type(): BlockType {
        return BlockType.LoopSelfJump;
    }

    // the public Type
    get loopSelfJumpType(): LoopSelfJumpType {
        if (!this.isModulation)
            return LoopSelfJumpType.SameKeyJoint;

        if (this.loop1.endIndex >= this.loop2.startIndex)
            return LoopSelfJumpType.ModulationOverlap;

        return this.jointMovement !== null
            ? LoopSelfJumpType.ModulationJointMovement
            : LoopSelfJumpType.ModulationAmbiguousChord;
    }

    get modulationDelta(): number {
        return Note.normalize(this.loop2.normalizationRoot - this.loop1.normalizationRoot);
    }

    /**
     * For {@link LoopSelfJumpType.SameKeyJoint} and {@link LoopSelfJumpType.ModulationJointMovement} type, the meanings are (fromNormalizedRootIndex, toNormalizedRootIndex).
     * For {@link LoopSelfJumpType.ModulationAmbiguousChord} type, the meanings are (equalNormalizedLoop1RootIndex, equalNormalizedLoop2RootIndex).
     * For {@link LoopSelfJumpType.ModulationOverlap} type, returns null.
     */
    get jumpPoints(): { readonly root1Index: number; readonly root2Index: number } | null {
        return this.loopSelfJumpType === LoopSelfJumpType.ModulationOverlap
            ? null
            : {
                root1Index: intMod(this.loop1.endIndex
                    - this.loop1.startIndex
                    + this.loop1.normalizationShift
                    + 1, this.loop1.loopLength),
                root2Index: this.loop2.normalizationShift,
            };
    }

    get overlapJumpPoints(): { readonly overlapStartRoot1Index: number; readonly overlapStartRoot2Index: number; readonly overlapLength: number } | null {
        return this.loopSelfJumpType !== LoopSelfJumpType.ModulationOverlap
            ? null
            : {
                overlapStartRoot1Index: intMod(this.loop2.startIndex
                    - this.loop1.startIndex
                    + this.loop1.normalizationShift, this.loop1.loopLength),
                overlapStartRoot2Index: this.loop2.normalizationShift,
                overlapLength: this.loop1.endIndex - this.loop2.startIndex + 1,
            };
    }
}
