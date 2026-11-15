import type { Artist } from '../Artist';
import type { Collection } from '../Collection';
import type { LikeCategory } from '../LikeCategory';
import type { User } from '../User';
import type { ISong } from './ISong';

export interface ICollection {
    readonly songs: readonly ISong[];
    readonly users: readonly User[];
    readonly artists: readonly Artist[];
    readonly likeCategories: readonly LikeCategory[];
    readonly etag: number | null;
    deepHash(): number[];
    clone(): Collection;
}
