import type { ILike } from '../ILike';

export interface ISearchableSong {
    readonly title: string;
    readonly additionalKeywords: string | null;
    readonly index: number;
    readonly likes: Iterable<ILike>;
}
