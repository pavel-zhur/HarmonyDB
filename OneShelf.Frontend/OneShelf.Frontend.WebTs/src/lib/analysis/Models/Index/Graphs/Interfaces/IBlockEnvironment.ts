import type { IIndexedBlock } from '../../Blocks/Interfaces/IIndexedBlock';
import type { BlockDetections } from '../../Enums/BlockDetections';
import type { IBlockJoint } from './IBlockJoint';

export interface IBlockEnvironment {
    readonly block: IIndexedBlock;
    readonly parents: readonly IBlockEnvironment[];
    readonly children: readonly IBlockEnvironment[];
    readonly childrenSubtree: readonly IBlockEnvironment[];
    readonly leftJoints: readonly IBlockJoint[];
    readonly rightJoints: readonly IBlockJoint[];
    readonly detections: BlockDetections;
}
