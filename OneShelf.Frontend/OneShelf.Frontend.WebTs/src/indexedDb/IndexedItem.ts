// The original: IndexedDb/IndexedItem.cs.
// Stored in IndexedDB as the JS interop serializes it (camelCase): { key, contents } — the store's key path is "key".

export interface IndexedItem {
    readonly key: string;
    /** The item's JSON (System.Text.Json, default options). */
    readonly contents: string;
}
