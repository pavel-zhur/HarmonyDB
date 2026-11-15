export interface ISearchableArtist {
    readonly name: string;
    readonly synonyms: Iterable<string>;
}
