import type { IBlockEnvironment } from './IBlockEnvironment';

export interface IBlockJoint {
    readonly block1: IBlockEnvironment;
    readonly block2: IBlockEnvironment;
    readonly overlapLength: number;
    readonly normalization: string;
    readonly isEdge: boolean;
}

/** The default interface member IBlockJoint.Score ([Obsolete]). */
export function blockJointScore(_joint: IBlockJoint): number {
    return 1;
}
