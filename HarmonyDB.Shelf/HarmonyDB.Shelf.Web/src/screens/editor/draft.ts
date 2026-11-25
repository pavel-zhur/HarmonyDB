// The editor's draft: what is typed is kept in this browser as it is typed — per person and per what is edited (new
// chords, these own chords, my variant of this song) — and comes back when the editor is opened again; it goes when
// the chords are saved or the draft is thrown away. As the local-first store keeps its queue: per account, in
// localStorage; attaching another account (or signing out) forgets every other person's drafts.

import type { OwnChordsText } from '../../data/ownChords';

const prefix = 'shelf.editorDraft.';

export interface Draft {
    title: string;
    artists: string;
    contents: string;
    description: string;
    key: string;
    bpm: string;
    visibility: OwnChordsText['visibility'];
    comment: string;
    also: string[];
    /** when it was typed last, ms */
    at: number;
}

const storageKey = (account: string, target: string) => `${prefix}${account}.${target}`;

export function readDraft(account: string, target: string): Draft | null {
    try {
        return JSON.parse(localStorage.getItem(storageKey(account, target)) ?? 'null') as Draft | null;
    } catch {
        return null;
    }
}

export function writeDraft(account: string, target: string, draft: Draft): void {
    try {
        localStorage.setItem(storageKey(account, target), JSON.stringify(draft));
    } catch {
        // no storage: the draft lives as long as the page
    }
}

export function forgetDraft(account: string, target: string): void {
    try {
        localStorage.removeItem(storageKey(account, target));
    } catch {
        // nothing kept
    }
}

/** Forgets the drafts of everyone but `keep` (null: everyone's). */
export function trimDrafts(keep: string | null): void {
    try {
        const mine = keep === null ? null : `${prefix}${keep}.`;
        const gone = Array.from({ length: localStorage.length }, (_, i) => localStorage.key(i))
            .filter((key): key is string => !!key && key.startsWith(prefix) && (mine === null || !key.startsWith(mine)));
        for (const key of gone) localStorage.removeItem(key);
    } catch {
        // no storage: nothing kept
    }
}
