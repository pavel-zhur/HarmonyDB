import { ArgumentOutOfRangeException } from '../../../../common/dotnet/exceptions';
import { intMod, toFloat } from '../../../../common/dotnet/Numbers';
import type { CompactHarmonyMovement } from '../../CompactV1/CompactHarmonyMovement';
import type { BlockType } from '../Enums/BlockType';
import type { IIndexedBlock } from './Interfaces/IIndexedBlock';

export interface LoopBlockBaseInit {
    loop: readonly CompactHarmonyMovement[];
    startIndex: number;
    endIndex: number;
    normalized: string;
    normalizationShift: number;
    normalizationRoot: number;
}

export abstract class LoopBlockBase implements IIndexedBlock {
    constructor(init: LoopBlockBaseInit) {
        this.loop = init.loop;
        this.startIndex = init.startIndex;
        this.endIndex = init.endIndex;
        this.normalized = init.normalized;
        this.normalizationShift = init.normalizationShift;
        this.normalizationRoot = init.normalizationRoot;
    }

    readonly loop: readonly CompactHarmonyMovement[];

    get loopLength(): number {
        return this.loop.length;
    }

    readonly startIndex: number;

    readonly endIndex: number;

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    readonly normalized: string;

    /**
     * Between 0 (inclusive) and progression length (exclusive).
     * Returns the index of the start of the original progression in the normalized progression.
     * In other words, the number of steps the original progression is ahead or the normalized progression is behind.
     * For invariants > 1, returns the minimal possible shift, i.e. between 0 (inclusive) and (progression length / invariants) (exclusive).
     */
    readonly normalizationShift: number;

    /**
     * Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
     * More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
     */
    readonly normalizationRoot: number;

    /**
     * Is a whole number of and only if edge chords are the same. Min = 1.
     * Greater than 1 if at least one movement repeats twice.
     * Equals 1 if all chords repeat once except the edge ones.
     */
    get occurrences(): number {
        return toFloat(this.blockLength / this.loopLength);
    }

    get occurrencesWhole(): boolean {
        return intMod(this.blockLength, this.loopLength) === 0;
    }

    /**
     * Is a whole number of and only if edge chords are the same. Min = 0.
     * Greater than 0 if at least one movement repeats twice.
     * Equals 0 if all chords repeat once except the edge ones.
     */
    get successions(): number {
        return toFloat(toFloat(this.blockLength / this.loopLength) - 1);
    }

    get successionsSignificant(): boolean {
        return this.blockLength >= this.loopLength * 2;
    }

    /**
     * Greater than 1. Is a whole if all chords repeat exactly the same number of times, like A B C D A B C D A B C D.
     * Might be a convenient indicator of significance (if value >= 2).
     */
    get eachChordCoveredTimes(): number {
        return toFloat((this.blockLength + 1) / this.loopLength);
    }

    get eachChordCoveredTimesWhole(): boolean {
        return intMod(this.blockLength + 1, this.loopLength) === 0;
    }

    get eachChordCoveredTimesSignificant(): boolean {
        return this.blockLength + 1 >= this.loopLength * 2;
    }

    get children(): readonly IIndexedBlock[] {
        return [];
    }

    getNormalizedCoordinate(index: number): string | null {
        if (index < this.startIndex || index > this.endIndex) throw new ArgumentOutOfRangeException(undefined, "(Parameter 'index')");
        return intMod(this.normalizationShift + index - this.startIndex, this.loopLength).toString(); // proven on paper :)
    }

    abstract readonly type: BlockType;
}
