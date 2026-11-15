import { ArgumentOutOfRangeException, InvalidOperationException } from '../../../../common/dotnet/exceptions';
import { at, atFromEnd, compareStrings, orderBy } from '../../../../common/dotnet/Linq';
import { once } from '../../../../common/OneShelf.Common/Extensions';
import { BlockType } from '../Enums/BlockType';
import { BlockJoint } from '../Graphs/BlockJoint';
import type { IIndexedBlock } from './Interfaces/IIndexedBlock';

export class PingPongBlock implements IIndexedBlock {
    constructor(init: { children: readonly IIndexedBlock[] }) {
        this.children = init.children;
    }

    readonly children: readonly IIndexedBlock[];

    get type(): BlockType {
        return BlockType.PingPong;
    }

    get normalized(): string {
        return orderBy(
            [...once(BlockJoint.getNormalization(at(this.children, 0), at(this.children, 1))), BlockJoint.getNormalization(at(this.children, 1), at(this.children, 2))],
            compareStrings)
            .join(', ');
    }

    get normalizationRoot(): number {
        throw new InvalidOperationException(
            'The ping pong block does not currently participate in other higher level blocks, and probably will not. So this property is not implemented.');
    }

    get startIndex(): number {
        return at(this.children, 0).startIndex;
    }

    get endIndex(): number {
        return atFromEnd(this.children, 1).endIndex;
    }

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    getNormalizedCoordinate(index: number): string | null {
        const matches = this.children.filter(c => index >= c.startIndex && index <= c.endIndex);
        if (!(matches.length === 1 || matches.length === 2))
            throw new ArgumentOutOfRangeException(undefined, "(Parameter 'index')");

        return matches.map(m => `${m.normalized}, ${m.getNormalizedCoordinate(index) ?? ''}`).join(', ');
    }
}
