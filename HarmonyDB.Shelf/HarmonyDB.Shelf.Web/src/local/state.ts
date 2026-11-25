// The pure part of the local-first store: what the server last said (the base), the changes not yet sent (the
// queue), what the person sees (the base with the queue on top), and how an answer of /api/sync is merged.
// No browser, no React here: local/state.test.ts tests it under node.

import type { Schemas } from '../api/client';

export type Change = Schemas['Change'];
export type MyMark = Schemas['MyMark'];
export type MyKey = Schemas['MyKey'];
export type MyNote = Schemas['MyNote'];
export type MyRecent = Schemas['MyRecent'];
export type SyncResponse = Schemas['SyncResponse'];
export type ChangeResult = Schemas['ChangeResult'];

/** A change made here, with the moment it was made (Change.at), waiting to be sent. */
export interface Queued {
    id: string;
    change: Change;
}

/** The person's own marks, keys, notes and recent, each under its key (below); only what is there now. */
export interface Base {
    marks: Record<string, MyMark>;
    keys: Record<string, MyKey>;
    notes: Record<string, MyNote>;
    recent: Record<string, MyRecent>;
    /** The watermark of the last answer: sent as `since` next time; null — ask for everything. */
    watermark: number | null;
}

export interface LocalState {
    base: Base;
    queue: Queued[];
    /** The ids of the changes sent lately: another tab's copy of the queue does not bring them back. */
    sent: string[];
}

export const emptyBase: Base = { marks: {}, keys: {}, notes: {}, recent: {}, watermark: null };
export const emptyState: LocalState = { base: emptyBase, queue: [], sent: [] };

const sentKept = 1000;

const sep = '\u0001';
export const markKey = (chords: string, kind: string) => chords + sep + kind;
export const keyKey = (chords: string, playlistId: string | null | undefined) => chords + sep + (playlistId ?? '');
export const noteKey = (chords: string | null | undefined, songId: string | null | undefined) => (chords ?? '') + sep + (songId ?? '');
export const recentKey = noteKey;

function putMark(view: Base, mark: MyMark) {
    const key = markKey(mark.chords, mark.kind);
    if (mark.on) view.marks[key] = mark;
    else delete view.marks[key];
}

function putKey(view: Base, key: MyKey) {
    const k = keyKey(key.chords, key.playlistId);
    if (key.semitones !== null && key.semitones !== undefined) view.keys[k] = key;
    else delete view.keys[k];
}

function putNote(view: Base, note: MyNote) {
    const k = noteKey(note.chords, note.songId);
    if (note.text) view.notes[k] = note;
    else delete view.notes[k];
}

function putRecent(view: Base, recent: MyRecent) {
    const k = recentKey(recent.chords, recent.songId);
    const old = view.recent[k];
    if (!old || old.openedAt < recent.openedAt) view.recent[k] = recent;
}

/** One change on a view, as the server would apply it. */
function apply(view: Base, change: Change) {
    const at = change.at ?? new Date().toISOString();
    switch (change.type) {
        case 'mark':
            putMark(view, { chords: change.chords!, kind: change.kind!, on: !!change.on, at });
            break;
        case 'key':
            putKey(view, { chords: change.chords!, playlistId: change.playlistId ?? null, semitones: change.semitones ?? null });
            break;
        case 'note':
            putNote(view, { chords: change.chords ?? null, songId: change.songId ?? null, text: change.text ?? null, updatedAt: at });
            break;
        case 'opened':
            putRecent(view, { chords: change.chords ?? null, songId: change.songId ?? null, openedAt: at });
            break;
    }
}

function copy(base: Base): Base {
    return { marks: { ...base.marks }, keys: { ...base.keys }, notes: { ...base.notes }, recent: { ...base.recent }, watermark: base.watermark };
}

/** What the person sees: the server's last word with the changes not yet sent on top, in their order. */
export function effective(state: LocalState): Base {
    if (state.queue.length === 0) return state.base;
    const view = copy(state.base);
    for (const queued of state.queue) apply(view, queued.change);
    return view;
}

/** The base after an answer: everything anew when it was asked from the start (`since` null), else the changes. */
export function mergeBase(base: Base, response: SyncResponse, asked: number | null): Base {
    const view = asked === null || asked > response.watermark ? copy(emptyBase) : copy(base);
    response.marks.forEach(mark => putMark(view, mark));
    response.keys.forEach(key => putKey(view, key));
    response.notes.forEach(note => putNote(view, note));
    response.recent.forEach(recent => putRecent(view, recent));
    view.watermark = response.watermark;
    return view;
}

export interface Outcome {
    id: string;
    change: Change;
    result: ChangeResult;
}

/**
 * The state after /api/sync answered the `sent` changes (asked with `since`): they leave the queue — applied or not;
 * one not applied as `stale` was overtaken by a newer change elsewhere, and the base already holds that newer state.
 * The changes made meanwhile stay queued.
 */
export function settle(state: LocalState, sent: Queued[], response: SyncResponse, since: number | null): { state: LocalState; outcomes: Outcome[] } {
    const sentIds = new Set(sent.map(x => x.id));
    const outcomes = sent.map((queued, i) => ({ id: queued.id, change: queued.change, result: response.results[i] ?? { applied: false, code: 'internal_error' } }));
    return {
        state: {
            base: mergeBase(state.base, response, since),
            queue: state.queue.filter(x => !sentIds.has(x.id)),
            sent: [...state.sent, ...sentIds].slice(-sentKept),
        },
        outcomes,
    };
}

/**
 * Two copies of one person's state (two tabs of the app): the queue is the union of both, in the order the changes
 * were made, without what either has sent; the base is the newer of the two (by its watermark).
 */
export function mergeStates(mine: LocalState, theirs: LocalState): LocalState {
    const sent = [...new Set([...mine.sent, ...theirs.sent])].slice(-sentKept);
    const done = new Set(sent);
    const queue = new Map<string, Queued>();
    for (const queued of [...mine.queue, ...theirs.queue]) if (!done.has(queued.id)) queue.set(queued.id, queued);
    const newer = (theirs.base.watermark ?? -1) > (mine.base.watermark ?? -1) ? theirs.base : mine.base;
    return {
        base: newer,
        queue: [...queue.values()].sort((a, b) => ((a.change.at ?? '') < (b.change.at ?? '') ? -1 : (a.change.at ?? '') > (b.change.at ?? '') ? 1 : 0)),
        sent,
    };
}

/** What is kept of a person who signed out: only what they changed and was not sent yet (it goes when they are back). */
export function signedOut(state: LocalState): LocalState {
    return { base: emptyBase, queue: state.queue, sent: [] };
}
