// The fingerings of a chord: the old frontend's model (resources/fingerings.json), split at build time into a small
// index and a piece per chord type and root (build/fingerings.ts), and its lookup (services/FingeringsProvider.ts,
// shared/FingeringIndicator.tsx): the chord's type found by its text on C, or by a synonym, then the piece of that
// type on the chord's root. The index is fetched the first time a fingering is asked for, each piece when its chord
// is; the worker keeps them for offline.

import indexUrl from 'virtual:shelf-fingerings';

// The model's names of the roots, A (0) to G# (11).
const noteNames = ['A', 'Bb', 'H', 'C', 'Cw', 'D', 'Dw', 'E', 'F', 'Fw', 'G', 'Gw'];

// What the model knows under another text.
const synonyms = new Map([
    ['C5+', 'C+'], ['Caug', 'C+'], ['Caug7', 'C75+'], ['Cmaj75-', 'Cmaj5-'], ['Cmaj7', 'Cmaj'], ['Csus', 'Csus4'],
    ['C6sus', 'C6sus4'], ['C7sus', 'C7sus4'], ['Cmmaj7', 'Cm7+'], ['Cdim', 'Cm5-'], ['Cadd2', 'Cadd9'], ['Cadd4', 'Cadd11'],
    ['Cmadd2', 'Cmadd9'], ['Cmadd4', 'Cmadd11'],
]);

const loaded = new Map<string, Promise<unknown>>();

/** A file of the model, once (a failed one is asked again next time). */
function load<T>(url: string): Promise<T> {
    let promise = loaded.get(url);
    if (!promise) {
        promise = fetch(url)
            .then(response => {
                if (!response.ok) throw new Error(`fingerings: ${response.status}`);
                return response.json();
            })
            .catch(error => {
                loaded.delete(url);
                throw error;
            });
        loaded.set(url, promise);
    }
    return promise as Promise<T>;
}

/** The pictures (data: addresses of GIFs) of a chord's fingerings; none when the model does not know the chord. */
export async function fingerings(note: number, fingering: string | null): Promise<string[]> {
    if (fingering === null) return [];
    const index = await load<Record<string, Record<string, string>>>(indexUrl);
    const text = 'C' + fingering;
    const piece = (index[text] ?? index[synonyms.get(text) ?? ''])?.[noteNames[note]!];
    if (!piece) return [];
    const pictures = await load<string[]>('/' + piece);
    return pictures.map(x => `data:image/gif;base64,${x}`);
}
