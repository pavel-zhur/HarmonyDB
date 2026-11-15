import { BlockType } from '../Enums/BlockType';
import type { IPolyBlock } from './Interfaces/IPolyBlock';
import { LoopBlockBase } from './LoopBlockBase';

export class PolyLoopBlock extends LoopBlockBase implements IPolyBlock {
    override get type(): BlockType {
        return BlockType.PolyLoop;
    }

    selfOverlapsDetected = false;
}
