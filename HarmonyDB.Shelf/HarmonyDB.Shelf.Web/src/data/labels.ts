// The sources of chords as this person is shown them (their labels), by the chords' ids — the device's own: the shelf's
// lists and song pages name chords by their ids only. Asked of the sources from the ids alone
// (POST /api/chords/labels): for the whole library each time the device takes it (offline/index.ts, which keeps them
// with the library), and once for chords seen elsewhere that the device has no label for.
//
//   const labelOf = useLabels(ids)        // the labels of these ids; those not known yet are asked (once)
//   setLabels(entries)                    // what the library's taking was told

import { useEffect, useSyncExternalStore } from 'react';
import { api, call, type Schemas } from '../api/client';

export type Label = Schemas['ChordsSource'];

/** The most ids one call asks for. */
const maxIds = 1000;

let known = new Map<string, Label>();
/** asked already (known or not): never asked again in this page's life */
const asked = new Set<string>();
let waiting = new Set<string>();
let timer: ReturnType<typeof setTimeout> | null = null;
const listeners = new Set<() => void>();

function changed(): void {
    known = new Map(known);
    listeners.forEach(listener => listener());
}

/** The labels the library's taking was told: they stand for those ids from now on. */
export function setLabels(entries: Iterable<readonly [string, Label]>): void {
    for (const [id, label] of entries) {
        known.set(id, label);
        asked.add(id);
    }
    changed();
}

/** Every label known, to keep with the library. */
export function allLabels(): [string, Label][] {
    return [...known];
}

/** Forgotten (another person on this device, or signed out). */
export function forgetLabels(): void {
    known = new Map();
    asked.clear();
    waiting = new Set();
    listeners.forEach(listener => listener());
}

async function askWaiting(): Promise<void> {
    timer = null;
    const ids = [...waiting];
    waiting = new Set();
    for (let i = 0; i < ids.length; i += maxIds) {
        try {
            const answer = await call(api.POST('/api/chords/labels', { body: { ids: ids.slice(i, i + maxIds) } }));
            for (const label of answer.labels) known.set(label.id, label.source);
            changed();
        } catch {
            // not answered now (no connection): asked again when the ids are seen again
            for (const id of ids.slice(i, i + maxIds)) asked.delete(id);
        }
    }
}

function ask(ids: readonly string[]): void {
    for (const id of ids) {
        if (asked.has(id)) continue;
        asked.add(id);
        waiting.add(id);
    }
    if (waiting.size && timer === null) timer = setTimeout(() => void askWaiting(), 30);
}

/** The labels of these chords as the device knows them; those it does not know are asked once. */
export function useLabels(ids: readonly string[]): (id: string) => Label | undefined {
    const map = useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => known);
    const key = ids.join('\n');
    useEffect(() => ask(ids), [key]);
    return id => map.get(id);
}

/** The different sources of these chords, as many as given, in their order. */
export function sourcesOf(labelOf: (id: string) => Label | undefined, ids: readonly string[], most = 3): Label[] {
    const seen = new Map<string, Label>();
    for (const id of ids) {
        const label = labelOf(id);
        if (label && !seen.has(label.key)) seen.set(label.key, label);
    }
    return [...seen.values()].slice(0, most);
}
