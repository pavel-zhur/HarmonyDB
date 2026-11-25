import type { Lookup } from '../../../../common/dotnet/Collections';
import type { IIndexedBlock } from '../Blocks/Interfaces/IIndexedBlock';
import type { IBlockEnvironment } from './Interfaces/IBlockEnvironment';
import type { IBlockJoint } from './Interfaces/IBlockJoint';

export class BlockGraph {
    constructor(init: {
        environments: ReadonlyMap<IIndexedBlock, IBlockEnvironment>;
        joints: readonly IBlockJoint[];
        blocksAt: Lookup<number, IIndexedBlock>;
        blocksByPositions: Lookup<{ readonly startIndex: number; readonly endIndex: number }, IIndexedBlock>;
    }) {
        this.environments = init.environments;
        this.joints = init.joints;
        this.blocksAt = init.blocksAt;
        this.blocksByPositions = init.blocksByPositions;
    }

    readonly environments: ReadonlyMap<IIndexedBlock, IBlockEnvironment>;

    readonly joints: readonly IBlockJoint[];

    readonly blocksAt: Lookup<number, IIndexedBlock>;

    readonly blocksByPositions: Lookup<{ readonly startIndex: number; readonly endIndex: number }, IIndexedBlock>;
}
