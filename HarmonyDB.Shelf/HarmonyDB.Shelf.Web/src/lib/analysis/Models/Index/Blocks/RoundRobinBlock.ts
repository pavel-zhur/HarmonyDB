import { at, atFromEnd, compareStrings, maxInt, minBy, range } from '../../../../common/dotnet/Linq';
import { intMod } from '../../../../common/dotnet/Numbers';
import { Out } from '../../../../common/dotnet/Out';
import { withIndices } from '../../../../common/OneShelf.Common/Extensions';
import { BlockType } from '../Enums/BlockType';
import { BlockJoint } from '../Graphs/BlockJoint';
import type { IIndexedBlock } from './Interfaces/IIndexedBlock';

export class RoundRobinBlock implements IIndexedBlock {
    constructor(children: readonly IIndexedBlock[], childrenPeriodLength: number) {
        this.children = children;
        this.childrenPeriodLength = childrenPeriodLength;

        this.level = maxInt(children.map(x => x instanceof RoundRobinBlock ? x.level : 0)) + 1;
        this.type = this.level === 1 ? BlockType.RoundRobin : BlockType.RoundRobinLx;

        const normalizationRoot = new Out<number>();
        this.normalized = RoundRobinBlock.getNormalization(this.children, this.childrenPeriodLength, normalizationRoot, this.level);
        this.normalizationRoot = normalizationRoot.value;
    }

    readonly children: readonly IIndexedBlock[];

    readonly type: BlockType;

    readonly normalized: string;

    readonly normalizationRoot: number;

    get startIndex(): number {
        return at(this.children, 0).startIndex;
    }

    get endIndex(): number {
        return atFromEnd(this.children, 1).endIndex;
    }

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    readonly childrenPeriodLength: number;

    readonly level: number;

    getNormalizedCoordinate(index: number): string | null {
        return this.children
            .filter(c => index >= c.startIndex && index <= c.endIndex)
            .map(m => `${m.normalized}, ${m.getNormalizedCoordinate(index) ?? ''}`)
            .join(', ');
    }

    /**
     * Returns joint-points-aware normalization, however, invariant against rotation.
     */
    static getNormalization(children: readonly IIndexedBlock[], distinctCount: number, normalizationRoot: Out<number>, level: number | null = null): string {
        const getNormalizationAt = (i: number) => BlockJoint.getNormalization(at(children, i), at(children, i + 1));

        const minChild = minBy(withIndices(children.slice(0, Math.max(0, distinctCount))), x => x.x.normalized, compareStrings).i;

        normalizationRoot.value = at(children, minChild).normalizationRoot;

        return [
            level?.toString() ?? '',
            ...range(minChild, distinctCount)
                .map(i => intMod(i, distinctCount))
                .map(getNormalizationAt),
        ].join(', ');
    }
}
