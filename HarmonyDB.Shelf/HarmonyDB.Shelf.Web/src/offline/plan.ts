// The pure part of taking the library's chords onto the device: what is still to take, in the server's pages, what an
// answer of the server does to the copy, and the answer kept for chords as their own reading gives it.

import type { Schemas } from '../api/client';
import { keys } from './lists';

type OfflineContent = Schemas['OfflineContent'];
type ChordsContent = Schemas['ChordsContentResponse'];

/** How many chords the server reads at once. */
export const batchSize = 50;

/** The library's chords the copy does not hold yet (and may: not those not open to the person), in the library's order. */
export function missing(library: readonly string[], held: (id: string) => boolean, closed: ReadonlySet<string> = new Set()): string[] {
    return library.filter(id => !held(id) && !closed.has(id));
}

export function chunks<T>(items: readonly T[], size: number): T[][] {
    const out: T[][] = [];
    for (let i = 0; i < items.length; i += size) out.push(items.slice(i, i + size));
    return out;
}

/**
 * Chords as the copy keeps them: what /api/chords/{id} answers, the sheet only — the others' marks, keys and notes on
 * them come with the chords' own reading online (the person's own are the local-first store's).
 */
export function keptChords(item: OfflineContent): ChordsContent {
    return { id: item.chords, state: 'open', header: item.header, content: item.content, marks: [], keys: [], myNotes: [] };
}

/**
 * What an answer does to the copy: open chords are kept; chords no longer open to the person, or no longer in the
 * library, leave it; chords whose source did not answer stay as they are (asked again at the next update).
 */
export function applyContents(items: readonly OfflineContent[]): { keep: { key: string; value: ChordsContent }[]; drop: string[]; later: string[]; closed: string[]; gone: string[] } {
    const keep: { key: string; value: ChordsContent }[] = [], drop: string[] = [], later: string[] = [], closed: string[] = [], gone: string[] = [];
    for (const item of items) {
        if (item.state === 'open') keep.push({ key: keys.chords(item.chords), value: keptChords(item) });
        else if (item.state === 'unavailable') later.push(item.chords);
        else {
            drop.push(keys.chords(item.chords));
            (item.state === 'notOpen' ? closed : gone).push(item.chords);
        }
    }
    return { keep, drop, later, closed, gone };
}

/** The pause before asking again after "too many requests": 2, 4, 8 … seconds, at most a minute. */
export function backoff(attempt: number): number {
    return Math.min(60_000, 2000 * 2 ** attempt);
}
