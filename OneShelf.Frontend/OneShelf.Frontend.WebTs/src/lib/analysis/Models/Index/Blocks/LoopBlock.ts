import { BlockType } from '../Enums/BlockType';
import { LoopBlockBase } from './LoopBlockBase';

export class LoopBlock extends LoopBlockBase {
    override get type(): BlockType {
        return BlockType.Loop;
    }
}
