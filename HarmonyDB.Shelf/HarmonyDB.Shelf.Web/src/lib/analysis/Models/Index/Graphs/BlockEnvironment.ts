import type { IIndexedBlock } from '../Blocks/Interfaces/IIndexedBlock';
import { BlockDetections } from '../Enums/BlockDetections';
import type { BlockJoint } from './BlockJoint';
import type { IBlockEnvironment } from './Interfaces/IBlockEnvironment';

export class BlockEnvironment implements IBlockEnvironment {
    constructor(init: { block: IIndexedBlock }) {
        this.block = init.block;
    }

    readonly block: IIndexedBlock;

    readonly parents: BlockEnvironment[] = [];
    readonly children: BlockEnvironment[] = [];
    readonly childrenSubtree: BlockEnvironment[] = [];
    readonly leftJoints: BlockJoint[] = [];
    readonly rightJoints: BlockJoint[] = [];

    detections: BlockDetections = BlockDetections.None;
}
