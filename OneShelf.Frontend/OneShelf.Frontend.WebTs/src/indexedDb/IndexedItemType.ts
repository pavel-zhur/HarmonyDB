// The original: IndexedDb/IndexedItemType.cs.

export enum IndexedItemType {
    Chords = 0,
    Search = 1,
    Progression = 2,
}

/** Enum.GetValues<IndexedItemType>() */
export const indexedItemTypes: readonly IndexedItemType[] = [IndexedItemType.Chords, IndexedItemType.Search, IndexedItemType.Progression];
