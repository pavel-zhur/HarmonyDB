import type { BlockType } from '../../Enums/BlockType';

export interface IBlock {
    readonly startIndex: number;

    readonly endIndex: number;

    readonly blockLength: number;

    readonly type: BlockType;
}
