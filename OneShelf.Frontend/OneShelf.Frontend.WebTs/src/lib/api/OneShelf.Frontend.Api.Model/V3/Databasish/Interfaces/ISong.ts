import type { DateTime } from '../../../../../common/dotnet/DateTime';
import type { ISearchableSong } from '../../../../../common/OneShelf.Common.Songs/FullTextSearch/ISearchableSong';
import type { Like } from '../Like';
import type { Song } from '../Song';
import type { Version } from '../Version';

export interface ISong extends ISearchableSong {
    readonly id: number;
    readonly artists: readonly number[];
    readonly createdByUserId: number;
    readonly createdOn: DateTime;
    readonly versions: readonly Version[];
    readonly likes: readonly Like[];
    readonly comments: ReadonlyMap<number, string>;
    readonly templateRating: number | null;
    deepHash(): number[];
    clone(): Song;
}
