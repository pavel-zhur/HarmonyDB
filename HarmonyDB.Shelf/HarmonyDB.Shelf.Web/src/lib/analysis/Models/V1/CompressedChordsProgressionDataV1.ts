import { Dictionary } from '../../../common/dotnet/Collections';
import { at, distinct } from '../../../common/dotnet/Linq';
import { withIndices } from '../../../common/OneShelf.Common/Extensions';
import type { ChordDataV1 } from './ChordDataV1';

export class CompressedChordsProgressionDataV1 {
    constructor(init: { sequence: number[]; distinct: ChordDataV1[] }) {
        this.sequence = init.sequence;
        this.distinct = init.distinct;
    }

    sequence: number[];

    distinct: ChordDataV1[];

    static compress(sequence: readonly ChordDataV1[]): CompressedChordsProgressionDataV1 {
        const distinctItems = distinct(sequence);
        const indices = new Dictionary<ChordDataV1, number>();
        for (const { x, i } of withIndices(distinctItems)) indices.add(x, i);
        return new CompressedChordsProgressionDataV1({
            distinct: distinctItems,
            sequence: sequence.map(x => indices.get(x)),
        });
    }

    decompress(): ChordDataV1[] {
        return this.sequence.map(x => at(this.distinct, x));
    }
}
